import os
import re
import json
import shutil
import xml.etree.ElementTree as ET

# Base directories relative to script location
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SITE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
MOD_ROOT = os.path.abspath(os.path.join(SITE_DIR, ".."))

DEFS_DIR = os.path.join(MOD_ROOT, "Common", "Defs")
TEXTURES_DIR = os.path.join(MOD_ROOT, "Common", "Textures")

OUTPUT_JSON_PATH = os.path.join(SITE_DIR, "data", "turrets_data.json")
OVERRIDES_JSON_PATH = os.path.join(SITE_DIR, "data", "image_overrides.json")
EXTRACTED_IMG_DIR = os.path.join(SITE_DIR, "images", "extracted")

os.makedirs(os.path.dirname(OUTPUT_JSON_PATH), exist_ok=True)
os.makedirs(EXTRACTED_IMG_DIR, exist_ok=True)

# Storage dictionaries for Defs
raw_defs_by_name = {}      # defName -> ET.Element
raw_defs_by_abstract = {}  # Name (Abstract) -> ET.Element
ammo_sets_by_name = {}     # defName -> AmmoSet dict
recipes_by_product = {}    # product defName -> Recipe dict
thing_categories = {}      # defName -> Category dict

# Load image overrides
image_overrides = {}
if os.path.exists(OVERRIDES_JSON_PATH):
    try:
        with open(OVERRIDES_JSON_PATH, "r", encoding="utf-8") as f:
            override_data = json.load(f)
            image_overrides = override_data.get("overrides", {})
    except Exception as e:
        print(f"Warning: Could not read image_overrides.json: {e}")

def parse_xml_files():
    """Traverse Common/Defs and load all XML elements into memory."""
    for root_dir, _, files in os.walk(DEFS_DIR):
        for f in files:
            if f.endswith(".xml") and not f.endswith(".bak"):
                file_path = os.path.join(root_dir, f)
                try:
                    tree = ET.parse(file_path)
                    root = tree.getroot()
                    if root.tag != "Defs":
                        continue
                    for elem in root:
                        if not isinstance(elem.tag, str):
                            continue
                        def_name = elem.findtext("defName")
                        abstract_name = elem.attrib.get("Name")
                        is_abstract = elem.attrib.get("Abstract", "false").lower() == "true"
                        
                        if abstract_name:
                            raw_defs_by_abstract[abstract_name] = elem
                        if def_name and not is_abstract:
                            raw_defs_by_name[def_name] = elem
                        
                        # Special handling for AmmoSetDef and RecipeDef
                        if "AmmoSetDef" in elem.tag:
                            def_name = elem.findtext("defName")
                            if def_name:
                                ammo_types = {}
                                ammo_types_elem = elem.find("ammoTypes")
                                if ammo_types_elem is not None:
                                    for entry in ammo_types_elem:
                                        ammo_types[entry.tag] = entry.text
                                ammo_sets_by_name[def_name] = {
                                    "defName": def_name,
                                    "label": elem.findtext("label"),
                                    "ammoTypes": ammo_types
                                }
                        elif elem.tag == "RecipeDef":
                            def_name = elem.findtext("defName")
                            products_elem = elem.find("products")
                            if products_elem is not None:
                                for prod in products_elem:
                                    recipes_by_product[prod.tag] = parse_recipe(elem)
                except Exception as e:
                    print(f"Error parsing XML file {file_path}: {e}")

def get_merged_xml(elem):
    """Recursively merge attributes and tags from ParentName hierarchy."""
    parent_name = elem.attrib.get("ParentName")
    if not parent_name or parent_name not in raw_defs_by_abstract:
        return elem
    
    parent_elem = raw_defs_by_abstract[parent_name]
    merged_parent = get_merged_xml(parent_elem)
    
    # Create combined element starting with parent
    combined = ET.Element(elem.tag, elem.attrib)
    for child in merged_parent:
        combined.append(ET.Element(child.tag, child.attrib))
        combined[-1].text = child.text
        combined[-1].extend(child)
        
    # Override/add child elements
    for child in elem:
        existing = combined.find(child.tag)
        if existing is not None:
            combined.remove(existing)
        combined.append(child)
        
    return combined

def parse_recipe(elem):
    """Extract crafting recipe details."""
    ingredients = []
    ing_elem = elem.find("ingredients")
    if ing_elem is not None:
        for li in ing_elem.findall("li"):
            count = li.findtext("count", "1")
            filter_elem = li.find("filter")
            items = []
            if filter_elem is not None:
                thing_defs = filter_elem.find("thingDefs")
                if thing_defs is not None:
                    items = [t.text for t in thing_defs.findall("li") if t.text]
            if items:
                ingredients.append({"item": items[0], "count": float(count)})
                
    work_amount = elem.findtext("workAmount", "0")
    products = {}
    products_elem = elem.find("products")
    if products_elem is not None:
        for prod in products_elem:
            products[prod.tag] = int(prod.text or "1")
            
    return {
        "workAmount": float(work_amount),
        "ingredients": ingredients,
        "products": products
    }

def find_image(tex_path, def_name):
    """Resolve texture image URL or copy texture to extracted images folder."""
    # 1. Check manual image overrides
    if def_name in image_overrides and image_overrides[def_name]:
        return image_overrides[def_name]
        
    if not tex_path:
        return None
        
    # Standardize texture path
    clean_path = tex_path.strip().replace("\\", "/")
    png_rel_path = clean_path + ".png"
    src_file = os.path.join(TEXTURES_DIR, png_rel_path)
    
    if os.path.exists(src_file):
        dest_filename = f"{def_name}.png"
        dest_file = os.path.join(EXTRACTED_IMG_DIR, dest_filename)
        try:
            shutil.copy2(src_file, dest_file)
            return f"images/extracted/{dest_filename}"
        except Exception as e:
            print(f"Error copying texture {src_file}: {e}")
            
    return None

def extract_comp_info(comps_elem, extensions_elem):
    """Extract specialized comps & mod extensions."""
    specialized = {
        "enclosed": None,
        "ciws": None,
        "variableRpm": None,
        "clamping": None,
        "ammoPreservation": None,
        "nonSnapRotation": None,
        "airburst": None,
        "guided": None,
        "shellingProps": None
    }
    
    # Process comps
    if comps_elem is not None:
        for li in comps_elem.findall("li"):
            cls = li.attrib.get("Class", "")
            comp_class = li.findtext("compClass", "")
            
            if "CompProperties_EnclosedTurret" in cls:
                specialized["enclosed"] = {
                    "bulletProtection": float(li.findtext("bulletProtection", "0")) * 100,
                    "explosiveProtection": float(li.findtext("explosiveProtection", "0")) * 100,
                    "temperatureProtection": float(li.findtext("temperatureProtection", "0")) * 100,
                    "hidePawnGraphics": li.findtext("hidePawnGraphics", "false").lower() == "true"
                }
            elif "CompProperties_TurretPreserveAmmo" in cls:
                if not specialized["ammoPreservation"]:
                    specialized["ammoPreservation"] = {}
                specialized["ammoPreservation"]["preserveAmmo"] = True
            elif "CompProperties_TurretSprayDiscipline" in cls:
                if not specialized["ammoPreservation"]:
                    specialized["ammoPreservation"] = {}
                specialized["ammoPreservation"]["sprayDiscipline"] = True
                
    # Process mod extensions
    if extensions_elem is not None:
        for li in extensions_elem.findall("li"):
            cls = li.attrib.get("Class", "")
            if "TurretClampingExtension" in cls:
                specialized["clamping"] = {
                    "minElevationAngle": float(li.findtext("minElevationAngle", "0")),
                    "maxVerticalDeviation": float(li.findtext("maxVerticalDeviation", "0")),
                    "maxRotationDeviation": float(li.findtext("maxRotationDeviation", "0")),
                    "minRotationAngle": float(li.findtext("minRotationAngle", "0")),
                    "maxRotationAngle": float(li.findtext("maxRotationAngle", "0"))
                }
            elif "TurretBarrelExtension" in cls:
                rpms = []
                rpms_elem = li.find("maxRPMs")
                if rpms_elem is not None:
                    rpms = [int(r.text) for r in rpms_elem.findall("li") if r.text]
                bursts = []
                bursts_elem = li.find("selectableBurstCounts")
                if bursts_elem is not None:
                    bursts = [int(b.text) for b in bursts_elem.findall("li") if b.text]
                specialized["variableRpm"] = {
                    "maxRPMs": rpms,
                    "selectableBurstCounts": bursts,
                    "spinDownTime": float(li.find("spinningAnimation").findtext("spindownTime", "0") if li.find("spinningAnimation") is not None else "0")
                }
            elif "NonSnapTurretExtension" in cls:
                specialized["nonSnapRotation"] = {
                    "turnSpeedDegreesPerTick": float(li.findtext("speed", "0")),
                    "turnSpeedDegreesPerSec": float(li.findtext("speed", "0")) * 60,
                    "preferredAngleRange": float(li.findtext("preferedAngleRange", "0"))
                }
            elif "TurretSprayDisciplineExtension" in cls:
                if not specialized["ammoPreservation"]:
                    specialized["ammoPreservation"] = {}
                specialized["ammoPreservation"]["shotsPerTarget"] = int(li.findtext("shotsPerTarget", "10"))
                specialized["ammoPreservation"]["cycleConeDegrees"] = float(li.findtext("cycleConeDegrees", "10"))
            elif "TurretTrackingExtension" in cls:
                if not specialized["ammoPreservation"]:
                    specialized["ammoPreservation"] = {}
                specialized["ammoPreservation"]["enableMidBurstTracking"] = li.findtext("enableMidBurstTracking", "false").lower() == "true"
                
    return specialized

def parse_gun_and_ammo(gun_def_name):
    """Extract weapon verbs, charges, magazine, and linked ammunition stats."""
    if gun_def_name not in raw_defs_by_name:
        return None, None, []
        
    raw_gun = raw_defs_by_name[gun_def_name]
    gun_elem = get_merged_xml(raw_gun)
    
    stat_bases = gun_elem.find("statBases")
    sights = float(stat_bases.findtext("SightsEfficiency", "1")) if stat_bases is not None else 1.0
    spread = float(stat_bases.findtext("ShotSpread", "0")) if stat_bases is not None else 0.0
    sway = float(stat_bases.findtext("SwayFactor", "0")) if stat_bases is not None else 0.0
    cooldown = float(stat_bases.findtext("RangedWeapon_Cooldown", "0")) if stat_bases is not None else 0.0
    
    verbs_elem = gun_elem.find("verbs")
    verb_info = {}
    ciws_info = None
    
    if verbs_elem is not None:
        for li in verbs_elem.findall("li"):
            cls = li.attrib.get("Class", "")
            if "VerbProperties_CIWS" in cls:
                ciws_info = {
                    "isCIWS": True,
                    "interceptionRange": float(li.findtext("range", "0")),
                    "burstCount": int(li.findtext("burstShotCount", "0")),
                    "ticksBetweenShots": int(li.findtext("ticksBetweenBurstShots", "1"))
                }
            elif "VerbPropertiesCE" in cls or "VerbProperties" in cls or not cls:
                warmup = float(li.findtext("warmupTime", "0"))
                min_range = float(li.findtext("minRange", "0"))
                max_range = float(li.findtext("range", "0"))
                burst_count = int(li.findtext("burstShotCount", "1"))
                ticks_between = int(li.findtext("ticksBetweenBurstShots", "0"))
                
                # RPM calculation: (60 / (ticks_between / 60)) or similar tick rate
                rpm = round((60.0 / (ticks_between / 60.0)) * burst_count, 1) if ticks_between > 0 else 0
                
                verb_info = {
                    "verbClass": li.findtext("verbClass", ""),
                    "warmupTime": warmup,
                    "minRange": min_range,
                    "range": max_range,
                    "burstShotCount": burst_count,
                    "ticksBetweenBurstShots": ticks_between,
                    "calculatedRPM": rpm,
                    "recoilAmount": float(li.findtext("recoilAmount", "0")),
                    "circularError": float(li.findtext("circularError", "0")),
                    "indirectFirePenalty": float(li.findtext("indirectFirePenalty", "0")),
                    "requireLineOfSight": li.findtext("requireLineOfSight", "true").lower() == "true"
                }

    # Ammo User Comp & Charges
    ammo_set_name = None
    mag_size = 0
    reload_time = 0
    charge_speeds = []
    
    comps_elem = gun_elem.find("comps")
    if comps_elem is not None:
        for li in comps_elem.findall("li"):
            cls = li.attrib.get("Class", "")
            if "CompProperties_AmmoUser" in cls:
                mag_size = int(li.findtext("magazineSize", "0"))
                reload_time = float(li.findtext("reloadTime", "0"))
                ammo_set_name = li.findtext("ammoSet")
            elif "CompProperties_Charges" in cls:
                speeds_elem = li.find("chargeSpeeds")
                if speeds_elem is not None:
                    charge_speeds = [float(s.text) for s in speeds_elem.findall("li") if s.text]

    # Parse ammo items from ammoSet
    ammunitions = []
    if ammo_set_name and ammo_set_name in ammo_sets_by_name:
        ammo_set = ammo_sets_by_name[ammo_set_name]
        for ammo_def_name, proj_def_name in ammo_set["ammoTypes"].items():
            ammo_data = parse_single_ammo(ammo_def_name, proj_def_name)
            if ammo_data:
                ammunitions.append(ammo_data)
                
    weapon_stats = {
        "gunDefName": gun_def_name,
        "sightsEfficiency": sights,
        "shotSpread": spread,
        "swayFactor": sway,
        "cooldown": cooldown,
        "magazineSize": mag_size,
        "reloadTime": reload_time,
        "chargeSpeeds": charge_speeds,
        "verb": verb_info,
        "ciws": ciws_info
    }
    
    return weapon_stats, ciws_info, ammunitions

def parse_single_ammo(ammo_def_name, proj_def_name):
    """Extract individual ammo def, recipe, and projectile stats."""
    if ammo_def_name not in raw_defs_by_name:
        return None
        
    ammo_elem = get_merged_xml(raw_defs_by_name[ammo_def_name])
    label = ammo_elem.findtext("label", ammo_def_name)
    description = ammo_elem.findtext("description", "")
    ammo_class = ammo_elem.findtext("ammoClass", "Standard")
    
    stat_bases = ammo_elem.find("statBases")
    market_value = float(stat_bases.findtext("MarketValue", "0")) if stat_bases is not None else 0.0
    mass = float(stat_bases.findtext("Mass", "0")) if stat_bases is not None else 0.0
    bulk = float(stat_bases.findtext("Bulk", "0")) if stat_bases is not None else 0.0
    
    # Graphic & Icon
    graphic_elem = ammo_elem.find("graphicData")
    tex_path = graphic_elem.findtext("texPath") if graphic_elem is not None else None
    image_url = find_image(tex_path, ammo_def_name)
    
    # Recipe
    recipe_info = recipes_by_product.get(ammo_def_name)
    
    # Projectile
    proj_info = parse_projectile(proj_def_name)
    
    return {
        "defName": ammo_def_name,
        "label": label,
        "description": description,
        "ammoClass": ammo_class,
        "marketValue": market_value,
        "mass": mass,
        "bulk": bulk,
        "imageUrl": image_url,
        "recipe": recipe_info,
        "projectile": proj_info
    }

def parse_projectile(proj_def_name):
    """Extract projectile speed, damage, AP, fragments, airburst, guided, and shelling props."""
    if proj_def_name not in raw_defs_by_name:
        return {"defName": proj_def_name}
        
    proj_elem = get_merged_xml(raw_defs_by_name[proj_def_name])
    thing_class = proj_elem.findtext("thingClass", "")
    
    speed = 0.0
    damage_def = "Bomb"
    damage_base = 0.0
    ap_sharp = 0.0
    ap_blunt = 0.0
    exp_radius = 0.0
    shake_factor = 0.0
    shelling_props = None
    
    proj_props = proj_elem.find("projectile")
    if proj_props is not None:
        speed = float(proj_props.findtext("speed", "0"))
        damage_def = proj_props.findtext("damageDef", "Bomb")
        damage_base = float(proj_props.findtext("damageAmountBase", "0"))
        ap_sharp = float(proj_props.findtext("armorPenetrationSharp", "0"))
        ap_blunt = float(proj_props.findtext("armorPenetrationBlunt", "0"))
        exp_radius = float(proj_props.findtext("explosionRadius", "0"))
        shake_factor = float(proj_props.findtext("screenShakeFactor", "0"))
        
        sp = proj_props.find("shellingProps")
        if sp is not None:
            shelling_props = {
                "range": float(sp.findtext("range", "0")),
                "tilesPerTick": float(sp.findtext("tilesPerTick", "0")),
                "damage": float(sp.findtext("damage", "1"))
            }

    # Secondary Explosive Comp
    sec_explosive = None
    fragments = None
    airburst = None
    
    comps_elem = proj_elem.find("comps")
    if comps_elem is not None:
        for li in comps_elem.findall("li"):
            cls = li.attrib.get("Class", "")
            if "CompProperties_ExplosiveCE" in cls:
                sec_explosive = {
                    "damage": float(li.findtext("damageAmountBase", "0")),
                    "damageDef": li.findtext("explosiveDamageType", "Bomb"),
                    "radius": float(li.findtext("explosiveRadius", "0"))
                }
            elif "CompProperties_Fragments" in cls:
                frag_list = []
                frags_elem = li.find("fragments")
                if frags_elem is not None:
                    for item in frags_elem:
                        frag_list.append({"type": item.tag, "count": int(item.text or "1")})
                fragments = {
                    "fragmentList": frag_list,
                    "fragAngleRange": li.findtext("fragAngleRange", "")
                }
            elif "CompProperties_AirburstFragments" in cls:
                airburst_frags = []
                frags_elem = li.find("fragments")
                if frags_elem is not None:
                    for item in frags_elem.findall("li"):
                        t_def = item.findtext("thingDef")
                        cnt = int(item.findtext("count", "1"))
                        if t_def:
                            airburst_frags.append({"type": t_def, "count": cnt})
                airburst = {
                    "subFragments": airburst_frags,
                    "fragAngleRange": li.findtext("fragAngleRange", ""),
                    "fragXZAngleRange": li.findtext("fragXZAngleRange", "")
                }

    # Mod Extensions for Airburst / Trajectory
    extensions_elem = proj_elem.find("modExtensions")
    if extensions_elem is not None:
        for li in extensions_elem.findall("li"):
            cls = li.attrib.get("Class", "")
            if "AirburstExtension" in cls:
                if not airburst:
                    airburst = {}
                airburst["type"] = li.findtext("type", "Flak")
                airburst["armingTicks"] = int(li.findtext("armingTicks", "0"))
                airburst["proximityRadius"] = float(li.findtext("proximityRadius", "0"))
                airburst["burstAltitude"] = float(li.findtext("burstAltitude", "0"))

    # Guided properties
    guided_props = None
    if proj_props is not None:
        if proj_props.findtext("guidanceOnDescending") or proj_props.findtext("homingAcceleration"):
            guided_props = {
                "guidanceOnDescending": proj_props.findtext("guidanceOnDescending", "false").lower() == "true",
                "homingAcceleration": float(proj_props.findtext("homingAcceleration", "0")),
                "retargetRadius": float(proj_props.findtext("retargetRadius", "0")),
                "guidanceDelay": float(proj_props.findtext("guidanceDelay", "0")),
                "trajectoryWorker": proj_props.findtext("trajectoryWorker", "")
            }

    return {
        "defName": proj_def_name,
        "thingClass": thing_class,
        "speed": speed,
        "damageDef": damage_def,
        "damageAmountBase": damage_base,
        "armorPenetrationSharp": ap_sharp,
        "armorPenetrationBlunt": ap_blunt,
        "explosionRadius": exp_radius,
        "screenShakeFactor": shake_factor,
        "secondaryExplosive": sec_explosive,
        "fragments": fragments,
        "airburst": airburst,
        "guided": guided_props,
        "shellingProps": shelling_props
    }

def determine_category_and_caliber(folder_name, def_name, label, desc):
    """Map turret folder and XML details into clean Sub-Category and Caliber string."""
    cat = "Cannons"
    if "Autocannons" in folder_name:
        cat = "Autocannons"
    elif "Howitzers" in folder_name:
        cat = "Howitzers"
    elif "Naval Guns" in folder_name or "NavalGuns" in folder_name:
        cat = "Naval Guns"
    elif "RotaryCannons" in folder_name or "Rotary" in folder_name:
        cat = "Rotary Cannons"
    elif "Unmanned" in folder_name:
        cat = "Unmanned"
        
    # Caliber extraction regex (e.g. 155mm, 30mm, 20x102mm, 76x636mm, 406mm, 600mm)
    caliber_match = re.search(r'(\d+mm|\d+x\d+mm|\d+x\d+)', label + " " + def_name + " " + desc)
    caliber = caliber_match.group(1) if caliber_match else "Cannon"
    
    return cat, caliber

def extract_all_turrets():
    """Main extraction routine for building unified turret objects."""
    parse_xml_files()
    
    building_defs = []
    # Identify all turret buildings
    for def_name, elem in raw_defs_by_name.items():
        building_elem = elem.find("building")
        if building_elem is not None and building_elem.find("turretGunDef") is not None:
            building_defs.append((def_name, elem))
            
    # Pair Direct and Indirect variants
    turret_groups = {} # group_id -> {"direct": elem, "indirect": elem}
    
    for def_name, elem in building_defs:
        if def_name.endswith("_indirect_Base"):
            base_id = def_name.replace("_indirect_Base", "")
            if base_id not in turret_groups:
                turret_groups[base_id] = {}
            turret_groups[base_id]["indirect"] = (def_name, elem)
        else:
            base_id = def_name.replace("_Base", "")
            if base_id not in turret_groups:
                turret_groups[base_id] = {}
            turret_groups[base_id]["direct"] = (def_name, elem)

    final_turrets_list = []

    for group_id, group in turret_groups.items():
        direct_tuple = group.get("direct")
        indirect_tuple = group.get("indirect")
        
        primary_def_name, primary_raw = direct_tuple if direct_tuple else indirect_tuple
        primary_elem = get_merged_xml(primary_raw)
        
        label = primary_elem.findtext("label", group_id)
        # Clean up indirect tag in primary label if needed
        clean_label = re.sub(r'\s*\(indirect\)', '', label, flags=re.IGNORECASE)
        description = primary_elem.findtext("description", "")
        
        # Folder sub-category
        folder_sub = ""
        for root_dir, _, files in os.walk(DEFS_DIR):
            for f in files:
                if f.endswith(".xml") and not f.endswith(".bak"):
                    # quick check if primary_def_name is in this file
                    with open(os.path.join(root_dir, f), "r", encoding="utf-8", errors="ignore") as file_obj:
                        if primary_def_name in file_obj.read():
                            folder_sub = root_dir
                            break
                            
        category, caliber = determine_category_and_caliber(folder_sub, primary_def_name, clean_label, description)
        
        # Image
        graphic_elem = primary_elem.find("graphicData")
        tex_path = graphic_elem.findtext("texPath") if graphic_elem is not None else None
        image_url = find_image(tex_path, primary_def_name)
        
        # Physical & Construction Stats
        stat_bases = primary_elem.find("statBases")
        hp = float(stat_bases.findtext("MaxHitPoints", "100")) if stat_bases is not None else 100
        work = float(stat_bases.findtext("WorkToBuild", "10000")) if stat_bases is not None else 10000
        mass = float(stat_bases.findtext("Mass", "1")) if stat_bases is not None else 1
        bulk = float(stat_bases.findtext("Bulk", "1")) if stat_bases is not None else 1
        
        size_str = primary_elem.findtext("size", "3,3").replace("(", "").replace(")", "")
        size_parts = [s.strip() for s in size_str.split(",") if s.strip()]
        size = [int(size_parts[0]), int(size_parts[1])] if len(size_parts) >= 2 else [3, 3]
        
        # Costs & Requirements
        costs = {}
        cost_elem = primary_elem.find("costList")
        if cost_elem is not None:
            for c in cost_elem:
                costs[c.tag] = int(c.text or "1")
                
        skill_req = int(primary_elem.findtext("constructionSkillPrerequisite", "0"))
        terrain = primary_elem.findtext("terrainAffordanceNeeded", "Heavy")
        
        research_reqs = []
        res_elem = primary_elem.find("researchPrerequisites")
        if res_elem is not None:
            research_reqs = [r.text for r in res_elem.findall("li") if r.text]
            
        # Comps & Extensions
        comps_elem = primary_elem.find("comps")
        ext_elem = primary_elem.find("modExtensions")
        specialized = extract_comp_info(comps_elem, ext_elem)
        
        power = 0
        if comps_elem is not None:
            for li in comps_elem.findall("li"):
                if "CompProperties_Power" in li.attrib.get("Class", ""):
                    power = float(li.findtext("basePowerConsumption", "0"))
                    
        # Direct vs Indirect Modes Processing
        direct_mode = None
        indirect_mode = None
        all_ammo_list = []
        ciws_global = None
        
        if direct_tuple:
            d_name, d_raw = direct_tuple
            d_elem = get_merged_xml(d_raw)
            d_gun_def = d_elem.find("building").findtext("turretGunDef")
            d_cooldown = float(d_elem.find("building").findtext("turretBurstCooldownTime", "0"))
            
            d_wstats, d_ciws, d_ammo = parse_gun_and_ammo(d_gun_def)
            if d_wstats:
                d_wstats["turretCooldown"] = d_cooldown
                direct_mode = d_wstats
            if d_ciws:
                ciws_global = d_ciws
            all_ammo_list.extend(d_ammo)
            
        if indirect_tuple:
            i_name, i_raw = indirect_tuple
            i_elem = get_merged_xml(i_raw)
            i_gun_def = i_elem.find("building").findtext("turretGunDef")
            i_cooldown = float(i_elem.find("building").findtext("turretBurstCooldownTime", "0"))
            
            i_wstats, i_ciws, i_ammo = parse_gun_and_ammo(i_gun_def)
            if i_wstats:
                i_wstats["turretCooldown"] = i_cooldown
                indirect_mode = i_wstats
            if i_ciws and not ciws_global:
                ciws_global = i_ciws
            all_ammo_list.extend(i_ammo)
            
        # Deduplicate ammunitions by defName
        unique_ammo = {}
        for a in all_ammo_list:
            if a["defName"] not in unique_ammo:
                unique_ammo[a["defName"]] = a
        ammo_final = list(unique_ammo.values())

        if ciws_global:
            specialized["ciws"] = ciws_global

        # Available Modes list
        fire_modes = []
        if direct_mode:
            fire_modes.append("Direct")
        if indirect_mode:
            fire_modes.append("Indirect")

        # Dynamic Feature Badges
        badges = []
        if len(fire_modes) > 1:
            badges.append("Dual Mode")
        if specialized["enclosed"]:
            badges.append("Enclosed Protection")
        if specialized["ciws"]:
            badges.append("CIWS Air Defense")
        if specialized["variableRpm"]:
            badges.append("Variable RPM")
        if specialized["clamping"]:
            badges.append("Turret Clamping")
        if specialized["ammoPreservation"]:
            badges.append("Smart Autoloader")
            
        # Check shelling capability in projectiles
        has_shelling = any(a.get("projectile", {}).get("shellingProps") for a in ammo_final)
        if has_shelling:
            badges.append("Cross-Map Shelling")
            
        # Check airburst capability
        has_airburst = any(a.get("projectile", {}).get("airburst") for a in ammo_final)
        if has_airburst:
            badges.append("Airburst / Flak")

        turret_entry = {
            "id": group_id,
            "defName": primary_def_name,
            "label": clean_label,
            "description": description,
            "category": category,
            "caliber": caliber,
            "imageUrl": image_url,
            "fireModes": fire_modes,
            "badges": badges,
            
            # Common Core Specs
            "common": {
                "maxHitPoints": hp,
                "workToBuild": work,
                "mass": mass,
                "bulk": bulk,
                "size": size,
                "basePowerConsumption": power,
                "constructionSkillPrerequisite": skill_req,
                "terrainAffordanceNeeded": terrain,
                "researchPrerequisites": research_reqs,
                "costList": costs
            },
            
            # Specialized Mechanics
            "specialized": specialized,
            
            # Fire Control Modes (Direct / Indirect)
            "modes": {
                "direct": direct_mode,
                "indirect": indirect_mode
            },
            
            # Ammunitions
            "ammunition": ammo_final
        }

        final_turrets_list.append(turret_entry)

    # Sort turrets by category and label
    final_turrets_list.sort(key=lambda x: (x["category"], x["label"]))
    
    # Save dataset to site/data/turrets_data.json
    output_data = {
        "generatedAt": os.popen("date /t").read().strip() if os.name == "nt" else "2026-08-28",
        "totalTurrets": len(final_turrets_list),
        "turrets": final_turrets_list
    }
    
    with open(OUTPUT_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(output_data, f, indent=2)

    print(f"SUCCESS: Successfully extracted {len(final_turrets_list)} turrets and saved to {OUTPUT_JSON_PATH}")

if __name__ == "__main__":
    extract_all_turrets()
