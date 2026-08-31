import os
import glob
import xml.etree.ElementTree as ET
import html
import json
import re

# Paths
MOD_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DEFS_DIR = os.path.join(MOD_ROOT, "Common", "Defs")
TEXTURES_DIR = os.path.join(MOD_ROOT, "Common", "Textures")
OUTPUT_HTML = os.path.join(MOD_ROOT, "docs", "turret_matrix.html")

def find_xml_files(directory):
    return glob.glob(os.path.join(directory, "**", "*.xml"), recursive=True)

def check_texture_exists(tex_path):
    if not tex_path:
        return True, ""
    if tex_path.startswith("Things/Building/Security/") or tex_path.startswith("Things/Item/"):
        return True, tex_path
    rel_path = tex_path.replace("/", os.sep).replace("\\", os.sep)
    full_path_png = os.path.join(TEXTURES_DIR, rel_path + ".png")
    if os.path.exists(full_path_png):
        return True, full_path_png
    return False, full_path_png

def extract_caliber_mm(label, def_name):
    text = f"{label} {def_name}"
    cm_match = re.search(r'(\d+(?:\.\d+)?)\s*cm', text, re.I)
    if cm_match:
        return float(cm_match.group(1)) * 10.0
    inch_match = re.search(r'(\d+(?:\.\d+)?)\s*(?:inch|in|")', text, re.I)
    if inch_match:
        return float(inch_match.group(1)) * 25.4
    mm_match = re.search(r'(\d+(?:\.\d+)?)\s*mm', text, re.I)
    if mm_match:
        return float(mm_match.group(1))
    num_match = re.search(r'(\d+)', label)
    if num_match:
        return float(num_match.group(1))
    return 0.0

class DefDatabase:
    def __init__(self):
        self.raw_defs = {}       # defName -> (ET.Element, filepath)
        self.named_bases = {}    # Name attribute -> (ET.Element, filepath)
        self.ammo_sets = {}      # ammoSet defName -> dict of ammo details
        self.projectiles = {}    # projectile defName -> dict of stats
        self.all_def_names = set()

    def parse_all(self):
        xml_files = find_xml_files(DEFS_DIR)
        
        for filepath in xml_files:
            try:
                tree = ET.parse(filepath)
                root = tree.getroot()
                if root.tag != "Defs":
                    continue
                for elem in root:
                    def_name = elem.findtext("defName")
                    name_attr = elem.get("Name")
                    
                    if def_name:
                        self.raw_defs[def_name] = (elem, filepath)
                        self.all_def_names.add(def_name)
                    if name_attr:
                        self.named_bases[name_attr] = (elem, filepath)

                    # Store Projectile stats
                    if elem.tag == "ThingDef":
                        proj_node = elem.find("projectile")
                        if proj_node is not None or elem.get("Name", "").endswith("Bullet"):
                            self.parse_projectile(elem, proj_node)

                    # Store AmmoSetDefs
                    if elem.tag == "CombatExtended.AmmoSetDef":
                        self.parse_ammo_set(elem)
            except Exception as e:
                print(f"Error parsing {filepath}: {e}")

    def parse_projectile(self, elem, proj_node):
        def_name = elem.findtext("defName")
        if not def_name:
            return
        
        label = elem.findtext("label", def_name)
        
        damage_base = "-"
        damage_def = "Bullet"
        armor_sharp = "-"
        armor_blunt = "-"
        speed = "-"
        explosion_radius = "-"

        if proj_node is not None:
            damage_base = proj_node.findtext("damageAmountBase", "-")
            damage_def = proj_node.findtext("damageDef", "Bullet")
            armor_sharp = proj_node.findtext("armorPenetrationSharp", "-")
            armor_blunt = proj_node.findtext("armorPenetrationBlunt", "-")
            speed = proj_node.findtext("speed", "-")
            explosion_radius = proj_node.findtext("explosionRadius", "-")

        # Secondary Damage
        secondary_damage = []
        if proj_node is not None:
            sec_node = proj_node.find("secondaryDamage")
            if sec_node is not None:
                for li in sec_node.findall("li"):
                    s_def = li.findtext("def", "")
                    s_amt = li.findtext("amount", "")
                    if s_def and s_amt:
                        secondary_damage.append(f"{s_def}:{s_amt}")

        # Fragments
        fragments = []
        comps_nodes = elem.findall("comps")
        for comps in comps_nodes:
            for li in comps.findall("li"):
                if "CompProperties_Fragments" in li.get("Class", ""):
                    f_node = li.find("fragments")
                    if f_node is not None:
                        for f_item in f_node:
                            tag_short = f_item.tag.replace("Fragment_", "")
                            fragments.append(f"{tag_short}x{f_item.text.strip()}")

        self.projectiles[def_name] = {
            "defName": def_name,
            "label": label,
            "damageAmountBase": damage_base,
            "damageDef": damage_def,
            "armorPenetrationSharp": armor_sharp,
            "armorPenetrationBlunt": armor_blunt,
            "speed": speed,
            "explosionRadius": explosion_radius,
            "secondaryDamage": ", ".join(secondary_damage),
            "fragments": ", ".join(fragments)
        }

    def parse_ammo_set(self, elem):
        def_name = elem.findtext("defName")
        if not def_name:
            return
        label = elem.findtext("label", def_name)
        ammo_types = []

        types_node = elem.find("ammoTypes")
        if types_node is not None:
            for child in types_node:
                ammo_item_def = child.tag
                proj_def = child.text.strip() if child.text else ""
                ammo_types.append({
                    "ammoItem": ammo_item_def,
                    "projectileDef": proj_def
                })

        self.ammo_sets[def_name] = {
            "defName": def_name,
            "label": label,
            "ammoTypes": ammo_types
        }

    def get_merged_element(self, elem, filepath):
        parent_name = elem.get("ParentName")
        if not parent_name:
            return elem

        parent_tuple = self.named_bases.get(parent_name) or self.raw_defs.get(parent_name)
        if not parent_tuple:
            return elem

        parent_elem, parent_filepath = parent_tuple
        merged_parent = self.get_merged_element(parent_elem, parent_filepath)
        
        merged = ET.Element(elem.tag, elem.attrib)
        for child in merged_parent:
            merged.append(ET.fromstring(ET.tostring(child)))
        for child in elem:
            existing = merged.find(child.tag)
            if existing is not None and len(child) == 0 and child.tag not in ["comps", "modExtensions", "costList", "placeWorkers", "statBases", "verbs"]:
                merged.remove(existing)
            merged.append(ET.fromstring(ET.tostring(child)))

        return merged

def get_all_child_lis(merged_elem, container_tag):
    items = []
    for container in merged_elem.findall(container_tag):
        for li in container.findall("li"):
            items.append(li)
    return items

def get_merged_child_text(merged_elem, path, default=""):
    parts = path.strip("./").split("/")
    if len(parts) == 1:
        for child in reversed(merged_elem):
            if child.tag == parts[0] and child.text is not None and child.text.strip():
                return child.text.strip()
        return default
    elif len(parts) == 2:
        container_tag, child_tag = parts
        for container in reversed(merged_elem.findall(container_tag)):
            val = container.findtext(child_tag)
            if val is not None and val.strip():
                return val.strip()
        return default
    return default

def get_merged_stat(merged_elem, stat_name, default="-"):
    for stat_node in reversed(merged_elem.findall("statBases")):
        val = stat_node.findtext(stat_name)
        if val is not None:
            return val.strip()
    return default

def parse_single_turret_def(elem, filepath, db):
    merged = db.get_merged_element(elem, filepath)
    def_name = elem.findtext("defName")
    turret_gun_def = get_merged_child_text(merged, "./building/turretGunDef")
    if not turret_gun_def:
        return None

    label = get_merged_child_text(merged, "label", def_name)
    parent_name = elem.get("ParentName", "")
    designator_dropdown = get_merged_child_text(merged, "designatorDropdown", "")

    # Graphic
    tex_path = get_merged_child_text(merged, "./graphicData/texPath", "")
    ui_icon_path = get_merged_child_text(merged, "uiIconPath", "")
    tex_ok, _ = check_texture_exists(tex_path)
    icon_ok, _ = check_texture_exists(ui_icon_path)

    # Building Stats
    hp = get_merged_stat(merged, "MaxHitPoints")
    work = get_merged_stat(merged, "WorkToBuild", get_merged_stat(merged, "WorkToMake"))
    mass = get_merged_stat(merged, "Mass")
    bulk = get_merged_stat(merged, "Bulk")
    skill_req = get_merged_child_text(merged, "constructionSkillPrerequisite", "-")
    cooldown_time = get_merged_child_text(merged, "./building/turretBurstCooldownTime", "-")
    top_draw_size = get_merged_child_text(merged, "./building/turretTopDrawSize", "-")

    # Costs
    costs = {}
    for cost_node in reversed(merged.findall("costList")):
        for item in cost_node:
            if item.tag not in costs:
                costs[item.tag] = item.text.strip() if item.text else "0"

    # Comps & Feature Dicts
    is_manned = False
    is_powered = False
    power_watts = "0"
    has_mode_swap = False
    swap_alt_def = ""
    swap_gizmo_label = ""
    has_fcs = False
    has_preserve_ammo = False
    has_suppression_immunity = False

    accuracy_override_params = {}
    fire_arc_params = {}
    smoker_params = {}

    has_mannable = False
    comp_lis = get_all_child_lis(merged, "comps")
    for comp in comp_lis:
        cls = comp.get("Class", "")
        if "CompProperties_Mannable" in cls or parent_name in ["AMCTurretMannedBase", "AMCArtilleryBase"]:
            has_mannable = True
        if "CompProperties_Power" in cls:
            is_powered = True
            p_val = comp.findtext("basePowerConsumption")
            if p_val:
                power_watts = p_val
        if "CompProperties_TurretModeSwap" in cls:
            has_mode_swap = True
            swap_alt_def = comp.findtext("alternateDef", "")
            swap_gizmo_label = comp.findtext("gizmoLabel", "")
            if comp.findtext("requiresFCS", "").lower() == "true":
                has_fcs = True
        if "CompProperties_TurretFCS" in cls:
            has_fcs = True
        if "CompProperties_TurretPreserveAmmo" in cls:
            has_preserve_ammo = True

        if "CompProperties_AccuracyOverride" in cls:
            for child in comp:
                if child.tag and child.text:
                    accuracy_override_params[child.tag] = child.text.strip()

        if "CompProperties_FireArc" in cls:
            min_arc = comp.findtext("./spanRange/min", "0")
            max_arc = comp.findtext("./spanRange/max", "360")
            fire_arc_params["minAngle"] = f"{min_arc}°"
            fire_arc_params["maxAngle"] = f"{max_arc}°"

        if "CompProperties_TurretSmoker" in cls:
            if comp.findtext("muzzleEnabled", "").lower() == "true":
                smoker_params["Muzzle Smoke"] = f"Vel: {comp.findtext('muzzleVelocity', '-')}, Size: {comp.findtext('muzzleParticleSize', '1')}"
            if comp.findtext("heatEnabled", "").lower() == "true":
                smoker_params["Heat Smoke"] = f"Thresh: {comp.findtext('heatThreshold', '-')}, Rate: {comp.findtext('heatEmissionRate', '-')}"
            if comp.findtext("shockwaveEnabled", "").lower() == "true":
                smoker_params["Shockwave"] = f"Rad: {comp.findtext('shockwaveRadius', '-')}, Density: {comp.findtext('shockwaveDensity', '-')}"

    if parent_name in ["AMCTurretMannedBase", "AMCArtilleryBase"]:
        has_mannable = True

    is_manned = has_mannable
    is_auto_parent = parent_name in ["AMCTurretAutoBase", "AMCArtilleryAutoBase"]
    ai_combat_dangerous = get_merged_child_text(merged, "./building/ai_combatDangerous", "").lower() == "true"

    # Mod Extensions & Animations
    ext_lis = get_all_child_lis(merged, "modExtensions")
    recoil_anim_params = {}
    firing_anim_params = {}
    selectable_bursts = []
    max_rpms = []

    for ext in ext_lis:
        ext_cls = ext.get("Class", "")
        
        recoil_node = ext.find("recoilAnimation")
        if recoil_node is not None:
            if recoil_node.findtext("enabled", "true").lower() == "true":
                for child in recoil_node:
                    if child.tag and child.text:
                        recoil_anim_params[child.tag] = child.text.strip()

        firing_node = ext.find("firingAnimation")
        if firing_node is not None:
            if firing_node.findtext("enabled", "true").lower() == "true":
                for child in firing_node:
                    if child.tag and child.text:
                        firing_anim_params[child.tag] = child.text.strip()

        burst_node = ext.find("selectableBurstCounts")
        if burst_node is not None:
            selectable_bursts = [li.text.strip() for li in burst_node.findall("li") if li.text]

        rpm_node = ext.find("maxRPMs")
        if rpm_node is not None:
            max_rpms = [li.text.strip() for li in rpm_node.findall("li") if li.text]

        if "TurretSuppressionImmunityExtension" in ext_cls:
            has_suppression_immunity = True

    # Detailed Barrel Extension Extraction
    barrel_ext = None
    for ext in ext_lis:
        if "TurretBarrelExtension" in ext.get("Class", ""):
            barrel_ext = ext
            break

    spinning_anim_params = {}
    if barrel_ext is not None:
        spin_node = barrel_ext.find("spinningAnimation")
        if spin_node is not None:
            spinning_anim_params = {
                "enabled": spin_node.findtext("enabled", "false").lower() == "true",
                "animationMode": spin_node.findtext("animationMode", "RPMBased"),
                "maxRPM": spin_node.findtext("maxRPM", "3000"),
                "spindownTime": spin_node.findtext("spindownTime", "1.5"),
                "frameCount": spin_node.findtext("frameCount", "4"),
                "barrelCount": spin_node.findtext("barrelCount", "6"),
                "spinUpSound": spin_node.findtext("spinUpSound", ""),
                "spinDownSound": spin_node.findtext("spinDownSound", "")
            }

    barrel_extension_data = {
        "hasBarrelExtension": barrel_ext is not None,
        "barrelDrawSize": barrel_ext.findtext("barrelDrawSize", "1.0") if barrel_ext is not None else "1.0",
        "barrelOffset": barrel_ext.findtext("barrelOffset", "(0,0,0.0)") if barrel_ext is not None else "(0,0,0.0)",
        "drawOnTop": barrel_ext.findtext("drawOnTop", "false").lower() == "true" if barrel_ext is not None else False,
        "barrelAmount": barrel_ext.findtext("barrelAmount", "1") if barrel_ext is not None else "1",
        "barrelSpacing": barrel_ext.findtext("barrelSpacing", "0.5") if barrel_ext is not None else "0.5",
        "sequentialFiring": barrel_ext.findtext("sequentialFiring", "false").lower() == "true" if barrel_ext is not None else False,
        "recoilAnimation": recoil_anim_params,
        "firingAnimation": firing_anim_params,
        "spinningAnimation": spinning_anim_params
    }

    # Detailed Smoker Extraction
    smoker_comp = None
    for comp in comp_lis:
        if "CompProperties_TurretSmoker" in comp.get("Class", ""):
            smoker_comp = comp
            break

    smoker_data = {
        "hasSmoker": smoker_comp is not None,
        "muzzleEnabled": smoker_comp.findtext("muzzleEnabled", "false").lower() == "true" if smoker_comp is not None else False,
        "muzzleFleckDef": smoker_comp.findtext("muzzleFleckDef", "AMC_MuzzleSmoke") if smoker_comp is not None else "AMC_MuzzleSmoke",
        "muzzleParticleCount": smoker_comp.findtext("muzzleParticleCount", "1") if smoker_comp is not None else "1",
        "muzzleVelocity": smoker_comp.findtext("muzzleVelocity", "15") if smoker_comp is not None else "15",
        "muzzleParticleSize": smoker_comp.findtext("muzzleParticleSize", "1~2") if smoker_comp is not None else "1~2",
        "heatEnabled": smoker_comp.findtext("heatEnabled", "false").lower() == "true" if smoker_comp is not None else False,
        "heatFleckDef": smoker_comp.findtext("heatFleckDef", "AMC_HeatSmoke") if smoker_comp is not None else "AMC_HeatSmoke",
        "heatThreshold": smoker_comp.findtext("heatThreshold", "40") if smoker_comp is not None else "40",
        "heatDecayRate": smoker_comp.findtext("heatDecayRate", "0.6") if smoker_comp is not None else "0.6",
        "heatEmissionRate": smoker_comp.findtext("heatEmissionRate", "1") if smoker_comp is not None else "1",
        "shockwaveEnabled": smoker_comp.findtext("shockwaveEnabled", "false").lower() == "true" if smoker_comp is not None else False,
        "shockwaveFleckDef": smoker_comp.findtext("shockwaveFleckDef", "AMC_ShockwaveSmoke") if smoker_comp is not None else "AMC_ShockwaveSmoke",
        "shockwaveRadius": smoker_comp.findtext("shockwaveRadius", "1") if smoker_comp is not None else "1",
        "shockwaveDensity": smoker_comp.findtext("shockwaveDensity", "2") if smoker_comp is not None else "2"
    }

    # Accuracy Override Extraction
    accuracy_override_data = {
        "hasAccuracyOverride": len(accuracy_override_params) > 0,
        "swayReduction": accuracy_override_params.get("swayReduction", "0"),
        "recoilReduction": accuracy_override_params.get("recoilReduction", "0"),
        "spreadReduction": accuracy_override_params.get("spreadReduction", "0")
    }

    # Weapon Def
    weapon_tuple = db.raw_defs.get(turret_gun_def)
    weapon_data = {}
    if weapon_tuple:
        w_elem, w_path = weapon_tuple
        w_merged = db.get_merged_element(w_elem, w_path)
        
        w_tex_path = get_merged_child_text(w_merged, "./graphicData/texPath", "")
        w_tex_ok, _ = check_texture_exists(w_tex_path)

        sights_eff = get_merged_stat(w_merged, "SightsEfficiency")
        shot_spread = get_merged_stat(w_merged, "ShotSpread")
        sway_factor = get_merged_stat(w_merged, "SwayFactor")
        cooldown = get_merged_stat(w_merged, "RangedWeapon_Cooldown")
        night_vision = get_merged_stat(w_merged, "NightVisionEfficiency_Weapon", get_merged_stat(merged, "NightVisionEfficiency", "-"))

        verb_class = ""
        min_range = "0"
        max_range = "0"
        burst_count = "1"
        ticks_between = "0"
        warmup_time = "0"
        is_ciws = False
        
        for v in get_all_child_lis(w_merged, "verbs"):
            v_cls = v.findtext("verbClass", "")
            if "Verb_Shoot" in v_cls:
                verb_class = v_cls.split(".")[-1]
                min_range = v.findtext("minRange", "0")
                max_range = v.findtext("range", "0")
                burst_count = v.findtext("burstShotCount", "1")
                ticks_between = v.findtext("ticksBetweenBurstShots", "0")
                warmup_time = v.findtext("warmupTime", "0")
            if "VerbCIWS" in v_cls or "CIWS" in v.get("Class", ""):
                is_ciws = True

        ammo_set = ""
        mag_size = "-"
        reload_time = "-"
        charge_speeds = []

        for wc in get_all_child_lis(w_merged, "comps"):
            wc_cls = wc.get("Class", "")
            if "CompProperties_AmmoUser" in wc_cls:
                ammo_set = wc.findtext("ammoSet", "")
                mag_size = wc.findtext("magazineSize", "-")
                reload_time = wc.findtext("reloadTime", "-")
            if "CompProperties_Charges" in wc_cls:
                c_node = wc.find("chargeSpeeds")
                if c_node is not None:
                    charge_speeds = [li.text.strip() for li in c_node.findall("li") if li.text]

        weapon_data = {
            "weaponDefName": turret_gun_def,
            "filePath": os.path.relpath(w_path, MOD_ROOT) if weapon_tuple else "",
            "rawXml": ET.tostring(w_elem, encoding='unicode') if weapon_tuple else "",
            "weaponTexPath": w_tex_path,
            "weaponTexOk": w_tex_ok,
            "sightsEfficiency": sights_eff,
            "shotSpread": shot_spread,
            "swayFactor": sway_factor,
            "cooldown": cooldown,
            "nightVision": night_vision,
            "verbClass": verb_class,
            "minRange": min_range,
            "maxRange": max_range,
            "burstShotCount": burst_count,
            "ticksBetweenBurstShots": ticks_between,
            "warmupTime": warmup_time,
            "isCiws": is_ciws,
            "ammoSet": ammo_set,
            "magazineSize": mag_size,
            "reloadTime": reload_time,
            "chargeSpeeds": charge_speeds
        }

    # Warnings & Audit Rules
    warnings = []
    if not tex_ok:
        warnings.append(f"Missing Base Texture: {tex_path}")
    if not icon_ok:
        warnings.append(f"Missing Menu Icon: {ui_icon_path}")
    if weapon_data and not weapon_data.get("weaponTexOk", True):
        warnings.append(f"Missing Gun Texture: {weapon_data['weaponTexPath']}")
    if has_mode_swap:
        if not swap_alt_def or swap_alt_def not in db.all_def_names:
            warnings.append(f"ModeSwap target def missing: {swap_alt_def}")

    # Unmanned Audit Checks
    is_unmanned_dir = "unmanned" in os.path.basename(os.path.dirname(filepath)).lower()
    if is_unmanned_dir or is_auto_parent:
        if not is_auto_parent:
            warnings.append("Unmanned turret must inherit from AMCTurretAutoBase or AMCArtilleryAutoBase")
        if not ai_combat_dangerous:
            warnings.append("Unmanned turret non-functional: Missing ai_combatDangerous=true")
        if has_mannable:
            warnings.append("Unmanned turret non-functional: Cannot have CompProperties_Mannable")
        if not is_powered:
            warnings.append("Unmanned turret expected to have power consumption comp")
        if not has_fcs:
            warnings.append("Unmanned turret expected to have FCS comp")

    return {
        "defName": def_name,
        "label": label,
        "parentName": parent_name,
        "designatorDropdown": designator_dropdown,
        "filePath": os.path.relpath(filepath, MOD_ROOT),
        "rawXml": ET.tostring(elem, encoding='unicode'),
        "hp": hp,
        "work": work,
        "mass": mass,
        "bulk": bulk,
        "skillReq": skill_req,
        "cooldownTime": cooldown_time,
        "topDrawSize": top_draw_size,
        "costs": costs,
        "isManned": is_manned,
        "isPowered": is_powered,
        "powerWatts": power_watts,
        "hasModeSwap": has_mode_swap,
        "swapAltDef": swap_alt_def,
        "swapGizmoLabel": swap_gizmo_label,
        "hasFcs": has_fcs,
        "hasPreserveAmmo": has_preserve_ammo,
        "accuracyOverride": accuracy_override_data,
        "smokerData": smoker_data,
        "barrelExtension": barrel_extension_data,
        "recoilAnimParams": recoil_anim_params,
        "firingAnimParams": firing_anim_params,
        "hasSelectableBursts": bool(selectable_bursts),
        "selectableBursts": selectable_bursts,
        "hasSuppressionImmunity": has_suppression_immunity,
        "weapon": weapon_data,
        "warnings": warnings
    }

def group_turret_systems(db):
    buildings_dir = os.path.join(DEFS_DIR, "ThingDefs_Buildings")
    turret_files = find_xml_files(buildings_dir)

    all_raw_turrets = {}

    for filepath in turret_files:
        rel_folder = os.path.basename(os.path.dirname(filepath))
        try:
            tree = ET.parse(filepath)
            root = tree.getroot()
            if root.tag != "Defs":
                continue
            for elem in root:
                if elem.tag != "ThingDef" or elem.get("Abstract") == "True":
                    continue
                def_data = parse_single_turret_def(elem, filepath, db)
                if def_data:
                    def_data['folder'] = rel_folder
                    all_raw_turrets[def_data['defName']] = def_data
        except Exception as e:
            print(f"Error reading {filepath}: {e}")

    visited = set()
    systems = []

    for def_name, t in all_raw_turrets.items():
        if def_name in visited:
            continue

        alt_def = t.get("swapAltDef")
        if alt_def and alt_def in all_raw_turrets:
            t_alt = all_raw_turrets[alt_def]
            visited.add(def_name)
            visited.add(alt_def)

            if "indirect" in def_name.lower():
                indirect_mode = t
                direct_mode = t_alt
            else:
                direct_mode = t
                indirect_mode = t_alt

            sys_label = direct_mode['label'].replace(" (Auto)", "").replace(" (indirect)", "").strip()

            systems.append({
                "systemLabel": sys_label,
                "systemType": "dual_mode",
                "folder": direct_mode['folder'],
                "hasModeSwap": True,
                "directMode": direct_mode,
                "indirectMode": indirect_mode,
                "primaryMode": direct_mode,
                "warnings": list(set(direct_mode['warnings'] + indirect_mode['warnings']))
            })
        else:
            visited.add(def_name)
            sys_label = t['label'].replace(" (Auto)", "").replace(" (indirect)", "").strip()
            is_indirect_only = "indirect" in def_name.lower() or "artillery" in t['label'].lower() or "mortar" in t['label'].lower()
            sys_type = "indirect_only" if is_indirect_only else "direct_only"
            systems.append({
                "systemLabel": sys_label,
                "systemType": sys_type,
                "folder": t['folder'],
                "hasModeSwap": False,
                "directMode": t if sys_type != "indirect_only" else None,
                "indirectMode": t if sys_type == "indirect_only" else None,
                "primaryMode": t,
                "warnings": t['warnings']
            })

    return systems

def generate_html(db, systems):
    systems.sort(key=lambda s: (extract_caliber_mm(s['systemLabel'], s['directMode']['defName'] if s.get('directMode') else s['primaryMode']['defName']), s['systemLabel']))

    db_export = {
        "systems": systems,
        "ammoSets": db.ammo_sets,
        "projectiles": db.projectiles,
        "allDefNames": sorted(list(db.all_def_names))
    }
    json_data = json.dumps(db_export, indent=2).replace("</script>", "<\\/script>")

    with open(os.path.join(os.path.dirname(__file__), "matrix_template.html"), "r", encoding="utf-8") as tf:
        template = tf.read()

    full_html = template.replace("<!-- DATA_PAYLOAD_HERE -->", json_data)

    os.makedirs(os.path.dirname(OUTPUT_HTML), exist_ok=True)
    with open(OUTPUT_HTML, "w", encoding="utf-8") as f:
        f.write(full_html)

    print(f"Successfully generated HTML Turret Matrix Editor at: {OUTPUT_HTML}")

if __name__ == "__main__":
    db = DefDatabase()
    db.parse_all()
    systems = group_turret_systems(db)
    generate_html(db, systems)
