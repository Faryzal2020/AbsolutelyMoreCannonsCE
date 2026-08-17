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

def get_merged_stat(merged_elem, stat_name, default="-"):
    for stat_node in merged_elem.findall("statBases"):
        val = stat_node.findtext(stat_name)
        if val is not None:
            return val
    return default

def parse_single_turret_def(elem, filepath, db):
    merged = db.get_merged_element(elem, filepath)
    def_name = elem.findtext("defName")
    turret_gun_def = merged.findtext("./building/turretGunDef")
    if not turret_gun_def:
        return None

    label = merged.findtext("label", def_name)
    parent_name = elem.get("ParentName", "")
    designator_dropdown = merged.findtext("designatorDropdown", "")

    # Graphic
    tex_path = merged.findtext("./graphicData/texPath", "")
    ui_icon_path = merged.findtext("uiIconPath", "")
    tex_ok, _ = check_texture_exists(tex_path)
    icon_ok, _ = check_texture_exists(ui_icon_path)

    # Building Stats
    hp = get_merged_stat(merged, "MaxHitPoints")
    work = get_merged_stat(merged, "WorkToBuild", get_merged_stat(merged, "WorkToMake"))
    mass = get_merged_stat(merged, "Mass")
    bulk = get_merged_stat(merged, "Bulk")
    skill_req = merged.findtext("constructionSkillPrerequisite", "-")
    cooldown_time = merged.findtext("./building/turretBurstCooldownTime", "-")
    top_draw_size = merged.findtext("./building/turretTopDrawSize", "-")

    # Costs
    costs = {}
    for cost_node in merged.findall("costList"):
        for item in cost_node:
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

    comp_lis = get_all_child_lis(merged, "comps")
    for comp in comp_lis:
        cls = comp.get("Class", "")
        if "CompProperties_Mannable" in cls or parent_name in ["AMCTurretMannedBase", "AMCArtilleryBase"]:
            is_manned = True
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
            is_manned = True

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
        is_manned = True

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

    # Weapon Def
    weapon_tuple = db.raw_defs.get(turret_gun_def)
    weapon_data = {}
    if weapon_tuple:
        w_elem, w_path = weapon_tuple
        w_merged = db.get_merged_element(w_elem, w_path)
        
        w_tex_path = w_merged.findtext("./graphicData/texPath", "")
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

    # Warnings
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

    return {
        "defName": def_name,
        "label": label,
        "parentName": parent_name,
        "designatorDropdown": designator_dropdown,
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
        "accuracyOverrideParams": accuracy_override_params,
        "fireArcParams": fire_arc_params,
        "smokerParams": smoker_params,
        "recoilAnimParams": recoil_anim_params,
        "firingAnimParams": firing_anim_params,
        "selectableBursts": selectable_bursts,
        "maxRPMs": max_rpms,
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
                "folder": direct_mode['folder'],
                "hasModeSwap": True,
                "directMode": direct_mode,
                "indirectMode": indirect_mode,
                "warnings": list(set(direct_mode['warnings'] + indirect_mode['warnings']))
            })
        else:
            visited.add(def_name)
            sys_label = t['label'].replace(" (Auto)", "").strip()
            systems.append({
                "systemLabel": sys_label,
                "folder": t['folder'],
                "hasModeSwap": False,
                "directMode": t,
                "indirectMode": None,
                "warnings": t['warnings']
            })

    return systems

def render_compact_ammo_table(ammo_set_name, mag_size, reload_time, db, mode_label=""):
    if not ammo_set_name:
        return "<div class='ammo-none'>No AmmoSet</div>"

    ammo_set_data = db.ammo_sets.get(ammo_set_name, {})
    ammo_types = ammo_set_data.get("ammoTypes", [])

    header_title = f"{mode_label}: <b>{html.escape(ammo_set_name)}</b>" if mode_label else f"<b>{html.escape(ammo_set_name)}</b>"
    
    html_out = f"""
        <div class="compact-ammo-block">
            <div class="ammo-header-line">
                <span class="ammo-set-title">📦 {header_title}</span>
                <span class="ammo-mag-badge">Mag: {mag_size} | {reload_time}s</span>
            </div>
    """

    if not ammo_types:
        html_out += "<div class='ammo-empty'>No ammo items registered</div></div>"
        return html_out

    html_out += """
        <table class="compact-ammo-table">
            <thead>
                <tr>
                    <th style="width: 28%;">Ammo Shell</th>
                    <th style="width: 18%;">Dmg</th>
                    <th style="width: 14%;">AP</th>
                    <th style="width: 12%;">Spd</th>
                    <th style="width: 28%;">Effects / Frags</th>
                </tr>
            </thead>
            <tbody>
    """

    for item in ammo_types:
        ammo_item_name = item['ammoItem']
        proj_name = item['projectileDef']
        proj = db.projectiles.get(proj_name, {})

        short_ammo_name = ammo_item_name.replace("Ammo_", "").replace("_Shells", "")

        dmg_val = proj.get("damageAmountBase", "-")
        dmg_type = proj.get("damageDef", "Bullet")
        ap_sharp = proj.get("armorPenetrationSharp", "-")
        spd = proj.get("speed", "-")
        
        dmg_class = "dmg-bullet"
        if dmg_type == "Bomb":
            dmg_class = "dmg-bomb"
        elif dmg_type in ["Flame", "Burn"]:
            dmg_class = "dmg-flame"

        sec = proj.get("secondaryDamage", "")
        rad = f"R:{proj['explosionRadius']}" if proj.get("explosionRadius") and proj.get("explosionRadius") != "-" else ""
        frag = proj.get("fragments", "")
        
        eff_parts = [p for p in [sec, rad, frag] if p]
        eff_str = ", ".join(eff_parts) if eff_parts else "-"

        ap_str = f"{ap_sharp}mm" if ap_sharp != "-" else "-"
        spd_str = f"{spd}" if spd != "-" else "-"

        html_out += f"""
            <tr>
                <td title="Item: {html.escape(ammo_item_name)}\nProj: {html.escape(proj_name)}">
                    <b class="ammo-name">{html.escape(short_ammo_name)}</b>
                </td>
                <td><span class="{dmg_class}">{dmg_val} {dmg_type}</span></td>
                <td><span class="ap-badge">{ap_str}</span></td>
                <td class="spd-text">{spd_str}</td>
                <td class="eff-text" title="{html.escape(eff_str)}">{html.escape(eff_str)}</td>
            </tr>
        """

    html_out += "</tbody></table></div>"
    return html_out

def render_feature_sub_table(title, param_dict):
    if not param_dict:
        return ""
    
    rows_html = ""
    for k, v in param_dict.items():
        rows_html += f"<tr><td>{html.escape(str(k))}</td><td>{html.escape(str(v))}</td></tr>"

    return f"""
        <div class="feature-table-block">
            <div class="feature-table-header">{title}</div>
            <table class="feature-sub-table">
                <tbody>{rows_html}</tbody>
            </table>
        </div>
    """

def render_collapsible_feature_table(title, param_dict, badge_icon="💥"):
    if not param_dict:
        return ""
    
    rows_html = ""
    for k, v in param_dict.items():
        rows_html += f"<tr><td>{html.escape(str(k))}</td><td>{html.escape(str(v))}</td></tr>"

    return f"""
        <details class="feature-details-block">
            <summary class="feature-summary-header">{badge_icon} {title} ({len(param_dict)} params)</summary>
            <table class="feature-sub-table">
                <tbody>{rows_html}</tbody>
            </table>
        </details>
    """

def generate_html(db, systems):
    # Sort turret systems by Caliber (mm) first, then systemLabel
    systems.sort(key=lambda s: (extract_caliber_mm(s['systemLabel'], s['directMode']['defName']), s['systemLabel']))

    total_turret_systems = len(systems)
    mode_swap_count = sum(1 for s in systems if s['hasModeSwap'])
    powered_count = sum(1 for s in systems if s['directMode']['isPowered'])
    recoil_count = sum(1 for s in systems if bool(s['directMode']['recoilAnimParams']) or bool(s['directMode']['firingAnimParams']))
    total_warnings = sum(len(s['warnings']) for s in systems)

    html_content = f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>AMC Turret Inspection & Feature Matrix</title>
    <style>
        :root {{
            --bg-color: #0f172a;
            --card-bg: #1e293b;
            --header-bg: #0f172a;
            --text-main: #f8fafc;
            --text-muted: #94a3b8;
            --accent: #38bdf8;
            --accent-green: #22c55e;
            --accent-red: #ef4444;
            --accent-amber: #f59e0b;
            --accent-purple: #a855f7;
            --border-color: #334155;
        }}

        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }}
        body {{ background-color: var(--bg-color); color: var(--text-main); padding: 15px; font-size: 12px; }}

        h1 {{ font-size: 20px; margin-bottom: 4px; color: var(--accent); display: flex; align-items: center; gap: 8px; }}
        .subtitle {{ color: var(--text-muted); margin-bottom: 15px; font-size: 12px; }}

        .metrics-grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-bottom: 20px; }}
        .metric-card {{ background: var(--card-bg); border: 1px solid var(--border-color); border-radius: 8px; padding: 12px; text-align: center; }}
        .metric-value {{ font-size: 24px; font-weight: bold; color: var(--accent); margin-top: 3px; }}
        .metric-label {{ font-size: 11px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; }}

        .controls-bar {{ background: var(--card-bg); border: 1px solid var(--border-color); border-radius: 8px; padding: 12px; margin-bottom: 15px; display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; }}
        .search-box input {{ background: #0f172a; border: 1px solid var(--border-color); color: white; padding: 6px 10px; border-radius: 6px; font-size: 12px; width: 240px; }}
        .filter-buttons {{ display: flex; gap: 6px; flex-wrap: wrap; }}
        .btn-filter {{ background: #334155; color: var(--text-main); border: none; padding: 5px 10px; border-radius: 5px; cursor: pointer; font-size: 11px; transition: all 0.2s; }}
        .btn-filter.active, .btn-filter:hover {{ background: var(--accent); color: #000; font-weight: bold; }}

        .table-container {{ overflow-x: auto; background: var(--card-bg); border: 1px solid var(--border-color); border-radius: 8px; width: 100%; }}
        table.main-table {{ width: 100%; border-collapse: collapse; text-align: left; table-layout: fixed; }}
        table.main-table th {{ background: #0f172a; color: var(--accent); font-weight: 600; text-transform: uppercase; font-size: 10px; letter-spacing: 0.5px; position: sticky; top: 0; z-index: 10; padding: 10px; border-bottom: 2px solid var(--border-color); }}
        table.main-table td {{ padding: 8px 10px; border-bottom: 1px solid var(--border-color); vertical-align: top; word-wrap: break-word; }}
        table.main-table > tbody > tr:hover {{ background: #243047; }}

        .badge {{ display: inline-block; padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: 600; margin: 2px; text-transform: uppercase; }}
        .bg-manned {{ background: rgba(34, 197, 94, 0.2); color: #4ade80; border: 1px solid #22c55e; }}
        .bg-auto {{ background: rgba(168, 85, 247, 0.2); color: #c084fc; border: 1px solid #a855f7; }}
        .bg-power {{ background: rgba(245, 158, 11, 0.2); color: #fbbf24; border: 1px solid #f59e0b; }}
        .bg-swap {{ background: rgba(56, 189, 248, 0.2); color: #38bdf8; border: 1px solid #38bdf8; }}
        .bg-recoil {{ background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid #ef4444; }}
        .bg-ciws {{ background: rgba(236, 72, 153, 0.2); color: #f472b6; border: 1px solid #ec4899; }}
        .bg-fcs {{ background: rgba(14, 165, 233, 0.2); color: #38bdf8; border: 1px solid #0284c7; }}
        .bg-feature {{ background: #334155; color: #cbd5e1; border: 1px solid #475569; }}

        .warning-badge {{ background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid #ef4444; padding: 3px 6px; border-radius: 4px; font-size: 10px; margin-top: 3px; display: block; }}
        .ok-badge {{ color: var(--accent-green); font-weight: bold; font-size: 11px; }}

        .mode-box {{ background: rgba(15, 23, 42, 0.6); border: 1px solid var(--border-color); border-radius: 5px; padding: 5px 7px; margin-bottom: 5px; font-size: 11px; }}
        .mode-title {{ font-weight: bold; color: var(--accent); font-size: 11px; margin-bottom: 2px; display: flex; justify-content: space-between; }}

        .code-text {{ font-family: monospace; color: #fbbf24; font-size: 11px; }}
        .cost-list {{ font-size: 11px; color: var(--text-muted); line-height: 1.3; }}

        /* Non-Boolean Feature Sub-Tables */
        .feature-table-block {{ margin-top: 6px; background: rgba(15, 23, 42, 0.6); border: 1px solid var(--border-color); border-radius: 5px; overflow: hidden; }}
        .feature-table-header {{ background: #1e293b; color: var(--accent); font-weight: 600; font-size: 10px; padding: 3px 6px; border-bottom: 1px solid var(--border-color); }}
        .feature-sub-table {{ width: 100%; border-collapse: collapse; font-size: 10px; table-layout: fixed; }}
        .feature-sub-table td {{ padding: 3px 6px; border-bottom: 1px solid rgba(51, 65, 85, 0.3); vertical-align: middle; word-wrap: break-word; }}
        .feature-sub-table td:first-child {{ color: var(--text-muted); font-weight: 500; width: 55%; }}
        .feature-sub-table td:last-child {{ color: #f8fafc; font-family: monospace; font-weight: 600; width: 45%; }}

        .feature-details-block {{ margin-top: 6px; background: rgba(15, 23, 42, 0.6); border: 1px solid var(--border-color); border-radius: 5px; overflow: hidden; }}
        .feature-summary-header {{ background: #1e293b; color: #fbbf24; font-weight: 600; font-size: 10px; padding: 4px 6px; cursor: pointer; user-select: none; border-bottom: 1px solid var(--border-color); }}
        .feature-summary-header:hover {{ background: #2d3d54; }}

        /* Compact Inline Ammo Table */
        .compact-ammo-block {{ margin-bottom: 6px; background: rgba(15, 23, 42, 0.5); border: 1px solid var(--border-color); border-radius: 5px; padding: 5px; width: 100%; }}
        .ammo-header-line {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; font-size: 10px; border-bottom: 1px solid rgba(51, 65, 85, 0.5); padding-bottom: 3px; }}
        .ammo-set-title {{ color: var(--accent); font-weight: 600; font-size: 10.5px; }}
        .ammo-mag-badge {{ background: #334155; color: #cbd5e1; padding: 1px 5px; border-radius: 3px; font-size: 9.5px; }}
        
        table.compact-ammo-table {{ width: 100%; border-collapse: collapse; font-size: 10px; table-layout: fixed; display: table !important; }}
        table.compact-ammo-table tr {{ display: table-row !important; }}
        table.compact-ammo-table th {{ background: #1e293b; color: var(--text-muted); font-weight: 600; padding: 3px 4px; text-align: left; font-size: 9px; text-transform: uppercase; border-bottom: 1px solid rgba(51, 65, 85, 0.5); }}
        table.compact-ammo-table td {{ padding: 3px 4px; border-bottom: 1px solid rgba(51, 65, 85, 0.3); vertical-align: middle; word-wrap: break-word; overflow: hidden; }}
        
        .ammo-name {{ color: #f8fafc; font-size: 10px; display: block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}
        .dmg-bullet {{ color: #f8fafc; }}
        .dmg-bomb {{ color: #f87171; font-weight: bold; }}
        .dmg-flame {{ color: #fbbf24; font-weight: bold; }}
        .ap-badge {{ color: #38bdf8; font-weight: 600; }}
        .spd-text {{ color: #94a3b8; }}
        .eff-text {{ color: #cbd5e1; font-size: 9.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; display: block; }}
        .ammo-none {{ color: var(--text-muted); font-style: italic; font-size: 10px; }}
        .ammo-empty {{ color: var(--text-muted); font-size: 10px; padding: 3px; }}
    </style>
</head>
<body>

    <h1>🚀 AMC Turret Inspection & Feature Matrix</h1>
    <div class="subtitle">Automated Developer Audit Matrix for Absolutely More Cannons (CE) — Sorted by Caliber (mm)</div>

    <div class="metrics-grid">
        <div class="metric-card">
            <div class="metric-label">Turret Systems</div>
            <div class="metric-value">{total_turret_systems}</div>
        </div>
        <div class="metric-card">
            <div class="metric-label">Mode Swap Capable</div>
            <div class="metric-value">{mode_swap_count}</div>
        </div>
        <div class="metric-card">
            <div class="metric-label">Powered Turrets</div>
            <div class="metric-value">{powered_count}</div>
        </div>
        <div class="metric-card">
            <div class="metric-label">Animated / Recoil</div>
            <div class="metric-value">{recoil_count}</div>
        </div>
        <div class="metric-card">
            <div class="metric-label">Audit Warnings</div>
            <div class="metric-value" style="color: {'var(--accent-red)' if total_warnings > 0 else 'var(--accent-green)'};">
                {total_warnings}
            </div>
        </div>
    </div>

    <div class="controls-bar">
        <div class="search-box">
            <input type="text" id="searchInput" placeholder="Search by caliber, defName, stat, ammo..." onkeyup="filterTable()">
        </div>
        <div class="filter-buttons">
            <button class="btn-filter active" onclick="filterCategory('all', this)">All Categories</button>
            <button class="btn-filter" onclick="filterCategory('Cannons', this)">Cannons</button>
            <button class="btn-filter" onclick="filterCategory('Howitzers', this)">Howitzers</button>
            <button class="btn-filter" onclick="filterCategory('Naval Guns', this)">Naval Guns</button>
            <button class="btn-filter" onclick="filterCategory('Autocannons', this)">Autocannons</button>
            <button class="btn-filter" onclick="filterCategory('RotaryCannons', this)">Rotary</button>
            <button class="btn-filter" onclick="filterCategory('Unmanned', this)">Unmanned</button>
            <button class="btn-filter" onclick="filterWarnings(this)" style="border-color: var(--accent-red);">⚠️ Warnings Only</button>
        </div>
    </div>

    <div class="table-container">
        <table id="turretTable" class="main-table">
            <thead>
                <tr>
                    <th style="width: 14%;">Turret System & Defs</th>
                    <th style="width: 14%;">Gameplay Stats & Costs</th>
                    <th style="width: 17%;">Firing & Ballistics</th>
                    <th style="width: 21%;">Features & Comps</th>
                    <th style="width: 26%;">AmmoSets & Projectiles</th>
                    <th style="width: 8%;">Audit</th>
                </tr>
            </thead>
            <tbody>
    """

    for s in systems:
        dm = s['directMode']
        im = s['indirectMode']

        folder = html.escape(s['folder'])
        sys_label = html.escape(s['systemLabel'])
        cal_mm = extract_caliber_mm(s['systemLabel'], dm['defName'])
        
        def_names_html = f"<div>Direct: <span class='code-text'>{html.escape(dm['defName'])}</span></div>"
        if im:
            def_names_html += f"<div>Indirect: <span class='code-text'>{html.escape(im['defName'])}</span></div>"

        cost_str = ", ".join([f"<b>{k}</b>:{v}" for k, v in dm['costs'].items()]) if dm['costs'] else "None"

        # Features Badges (Boolean Flags)
        badges = []
        if dm['isManned']:
            badges.append('<span class="badge bg-manned">🧑 Manned</span>')
        else:
            badges.append('<span class="badge bg-auto">🤖 Automatic</span>')

        if dm['isPowered']:
            badges.append(f'<span class="badge bg-power">⚡ {dm["powerWatts"]}W</span>')
        else:
            badges.append('<span class="badge bg-feature">Unpowered</span>')

        if s['hasModeSwap']:
            badges.append('<span class="badge bg-swap">🔄 Mode Swap</span>')
        else:
            badges.append('<span class="badge bg-feature">Direct-Only</span>')

        if dm['hasFcs'] or (im and im['hasFcs']):
            badges.append('<span class="badge bg-fcs">🖥️ FCS</span>')

        if dm['hasSuppressionImmunity']:
            badges.append('<span class="badge bg-feature">🛡️ Operator Immunity</span>')

        w_dm = dm['weapon']
        if w_dm.get('isCiws'):
            badges.append('<span class="badge bg-ciws">🛡️ CIWS</span>')

        badges_html = " ".join(badges)

        # Structured Sub-Tables for Non-Boolean Features
        feature_tables_html = ""

        # 1. Accuracy Override Table
        if dm.get("accuracyOverrideParams"):
            feature_tables_html += render_feature_sub_table("🎯 Accuracy Override", dm["accuracyOverrideParams"])

        # 2. Fire Arc Table
        if dm.get("fireArcParams"):
            feature_tables_html += render_feature_sub_table("📐 Fire Arc", dm["fireArcParams"])

        # 3. Turret Smoker Table
        if dm.get("smokerParams"):
            feature_tables_html += render_feature_sub_table("💨 Turret Smoker", dm["smokerParams"])

        # 4. Burst & Rate Settings Table
        burst_rpm_dict = {}
        if dm.get("selectableBursts"):
            burst_rpm_dict["Burst Modes"] = ", ".join(dm["selectableBursts"])
        if dm.get("maxRPMs"):
            burst_rpm_dict["Max RPM Modes"] = ", ".join(dm["maxRPMs"])
        if burst_rpm_dict:
            feature_tables_html += render_feature_sub_table("🎛️ Burst & Rate Settings", burst_rpm_dict)

        # 5. Collapsible Recoil Animation Table
        if dm.get("recoilAnimParams"):
            feature_tables_html += render_collapsible_feature_table("Recoil Animation", dm["recoilAnimParams"], badge_icon="💥")

        # 6. Collapsible Firing Animation Table
        if dm.get("firingAnimParams"):
            feature_tables_html += render_collapsible_feature_table("Firing Animation", dm["firingAnimParams"], badge_icon="🔥")

        # Firing Ballistics
        firing_html = ""
        r_min = w_dm.get('minRange', '0')
        r_max = w_dm.get('maxRange', '0')
        b_count = w_dm.get('burstShotCount', '1')
        b_cooldown = dm['cooldownTime']
        r_cooldown = w_dm.get('cooldown', '-')
        shot_spread = w_dm.get('shotSpread', '-')
        sights_eff = w_dm.get('sightsEfficiency', '-')
        nv_eff = w_dm.get('nightVision', '-')

        firing_html += f"""
            <div class="mode-box">
                <div class="mode-title"><span>🎯 Direct Mode</span> <span class="code-text">{w_dm.get('verbClass', '-')}</span></div>
                <div><b>Range:</b> {r_min} - {r_max} cells</div>
                <div><b>Burst / Cooldown:</b> {b_count} shots | {b_cooldown}s (b) / {r_cooldown}s (w)</div>
                <div><b>Stats:</b> Sights: {sights_eff} | Spread: {shot_spread} | NV: {nv_eff}</div>
            </div>
        """

        if im:
            w_im = im['weapon']
            ir_min = w_im.get('minRange', '0')
            ir_max = w_im.get('maxRange', '0')
            ib_count = w_im.get('burstShotCount', '1')
            ib_cooldown = im['cooldownTime']
            ir_cooldown = w_im.get('cooldown', '-')
            ishot_spread = w_im.get('shotSpread', '-')
            isights_eff = w_im.get('sightsEfficiency', '-')
            inv_eff = w_im.get('nightVision', '-')
            charges_str = f"| Charges: {', '.join(w_im.get('chargeSpeeds', []))}" if w_im.get('chargeSpeeds') else ""

            firing_html += f"""
                <div class="mode-box">
                    <div class="mode-title"><span style="color:#c084fc;">🌐 Indirect Mode</span> <span class="code-text">{w_im.get('verbClass', '-')}</span></div>
                    <div><b>Range:</b> {ir_min} - {ir_max} cells {charges_str}</div>
                    <div><b>Burst / Cooldown:</b> {ib_count} shots | {ib_cooldown}s (b) / {ir_cooldown}s (w)</div>
                    <div><b>Stats:</b> Sights: {isights_eff} | Spread: {ishot_spread} | NV: {inv_eff}</div>
                </div>
            """

        # Ammo Column (Compact Inline Tables)
        ammo_column_html = ""
        ammo_set_direct = w_dm.get('ammoSet', '')
        mag_direct = w_dm.get('magazineSize', '-')
        reload_direct = w_dm.get('reloadTime', '-')

        ammo_column_html += render_compact_ammo_table(ammo_set_direct, mag_direct, reload_direct, db, "Direct" if im else "")

        if im:
            w_im = im['weapon']
            ammo_set_indirect = w_im.get('ammoSet', '')
            mag_indirect = w_im.get('magazineSize', '-')
            reload_indirect = w_im.get('reloadTime', '-')
            ammo_column_html += render_compact_ammo_table(ammo_set_indirect, mag_indirect, reload_indirect, db, "Indirect")

        # Warnings
        if s['warnings']:
            audit_html = "".join([f'<span class="warning-badge">⚠️ {html.escape(warn)}</span>' for warn in s['warnings']])
        else:
            audit_html = '<span class="ok-badge">✔ Valid</span>'

        has_warning_attr = "true" if s['warnings'] else "false"

        html_content += f"""
            <tr data-category="{folder}" data-warning="{has_warning_attr}" data-caliber="{cal_mm}">
                <td>
                    <div style="font-weight: bold; color: var(--accent); font-size: 13px;">
                        {sys_label} <span style="font-size: 10px; color: #fbbf24; font-weight: normal;">({cal_mm:g}mm)</span>
                    </div>
                    {def_names_html}
                    <div style="font-size: 10px; color: var(--text-muted); margin-top: 2px;">
                        📂 {folder} | Parent: <span style="color: #cbd5e1;">{dm['parentName']}</span>
                    </div>
                </td>
                <td>
                    <div><b>HP:</b> {dm['hp']} | <b>Work:</b> {dm['work']}</div>
                    <div><b>Mass:</b> {dm['mass']}kg | <b>Bulk:</b> {dm['bulk']}</div>
                    <div class="cost-list" style="margin-top: 3px;"><b>Cost:</b> {cost_str}</div>
                </td>
                <td>{firing_html}</td>
                <td>
                    {badges_html}
                    {feature_tables_html}
                </td>
                <td>{ammo_column_html}</td>
                <td>{audit_html}</td>
            </tr>
        """

    html_content += f"""
            </tbody>
        </table>
    </div>

    <script>
        function filterTable() {{
            const input = document.getElementById('searchInput').value.toLowerCase();
            const rows = document.querySelectorAll('#turretTable > tbody > tr');
            
            rows.forEach(row => {{
                const text = row.innerText.toLowerCase();
                row.style.display = text.includes(input) ? '' : 'none';
            }});
        }}

        function filterCategory(cat, btn) {{
            document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            const rows = document.querySelectorAll('#turretTable > tbody > tr');
            rows.forEach(row => {{
                if (cat === 'all' || row.getAttribute('data-category') === cat) {{
                    row.style.display = '';
                }} else {{
                    row.style.display = 'none';
                }}
            }});
        }}

        function filterWarnings(btn) {{
            document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            const rows = document.querySelectorAll('#turretTable > tbody > tr');
            rows.forEach(row => {{
                if (row.getAttribute('data-warning') === 'true') {{
                    row.style.display = '';
                }} else {{
                    row.style.display = 'none';
                }}
            }});
        }}
    </script>
</body>
</html>
    """

    os.makedirs(os.path.dirname(OUTPUT_HTML), exist_ok=True)
    with open(OUTPUT_HTML, "w", encoding="utf-8") as f:
        f.write(html_content)

    print(f"Successfully generated HTML Turret Matrix at: {OUTPUT_HTML}")

if __name__ == "__main__":
    db = DefDatabase()
    db.parse_all()
    systems = group_turret_systems(db)
    generate_html(db, systems)
