#!/usr/bin/env python3
"""
deploy_modules.py
-----------------
Automated build & export script for "Absolutely More Cannons - CE".
Splits the AiO (Dev) source mod repository into 6 modular release mods + 1 AiO release mod:
  1. Absolutely More Cannons - Core
  2. Absolutely More Cannons - Autocannons
  3. Absolutely More Cannons - Cannons
  4. Absolutely More Cannons - Howitzers
  5. Absolutely More Cannons - Naval Guns
  6. Absolutely More Cannons - Rotary Cannons
  7. Absolutely More Cannons - Complete (AiO)

Usage:
  python DevTools/deploy_modules.py
"""

import os
import sys
import shutil
import xml.etree.ElementTree as ET

# Define paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEV_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
MODS_DIR = os.path.dirname(DEV_ROOT)  # E:\SteamLibrary\steamapps\common\RimWorld\Mods

print(f"[Deploy] Source (Dev) Root: {DEV_ROOT}")
print(f"[Deploy] Output Mods Root:  {MODS_DIR}\n")

MODULES_SPEC = {
    "Autocannons": {
        "dir_name": "Absolutely More Cannons - Autocannons",
        "package_id": "faryzal2020.amc.autocannons",
        "display_name": "Absolutely More Cannons - Autocannons",
        "description": "Autocannon turrets module for Absolutely More Cannons CE. Requires AMC Core.",
        "buildings_dir": "Autocannons",
        "unmanned_xmls": ["U20mmFlak38.xml", "U20mmSPzMarder.xml", "U40mmDCA40.xml"],
        "ammo_xmls": [
            "20x138mm.xml", "20x139mm.xml", "25x137mm.xml", "30x165mm.xml",
            "30x170mm.xml", "30x184mm.xml", "37x249mm.xml", "40x365mm.xml"
        ],
        "research_xml": "RP_Autocannons.xml",
        "sound_xml": "Autocannons.xml",
        "texture_prefixes": [
            "M20mmFlak38", "U20mmFlak38", "M20mmSPzMarder", "U20mmSPzMarder",
            "M25mmSIDAM", "M30mmBMP2", "M30mmFV510", "M30mmKugelblitz",
            "M37mmFlakM42", "M40mmDCA40", "U40mmDCA40", "M40mmM247"
        ],
        "weapon_defnames": [
            "Turret_20mmFlak38_Weapon", "Turret_U20mmFlak38_Weapon",
            "Turret_20mmSPzMarder_Weapon", "Turret_25mmSIDAM_Weapon",
            "Turret_30mmBMP2_Weapon", "Turret_30mmFV510_Weapon",
            "Turret_30mmKugelblitz_Weapon", "Turret_37mmFlakM42_Weapon",
            "Turret_40mmDCA40_Weapon", "Turret_40mmM247_Weapon"
        ],
        "autoloader_ammo_sets": {
            "AMC_Autoloader_Small": [
                "AmmoSet_20x138mmB", "AmmoSet_20x139mm_Shells",
                "AmmoSet_25x137mmNATO", "AmmoSet_30x165mm_Shells",
                "AmmoSet_30x170mm_Shells", "AmmoSet_30x184mm_Shells",
                "AmmoSet_37x249mm_Shells", "AmmoSet_40x365mmBofors"
            ]
        }
    },
    "Cannons": {
        "dir_name": "Absolutely More Cannons - Cannons",
        "package_id": "faryzal2020.amc.cannons",
        "display_name": "Absolutely More Cannons - Cannons",
        "description": "Direct fire heavy cannons module for Absolutely More Cannons CE. Requires AMC Core.",
        "buildings_dir": "Cannons",
        "unmanned_xmls": ["U57mmDeacon.xml"],
        "ammo_xmls": [
            "57mmOQF.xml", "75mmPAK40.xml", "88mmFLAK41.xml", "90mmM26.xml",
            "100mmBS3.xml", "105mmL7A3.xml", "106mmM40.xml", "128mmPAK44.xml"
        ],
        "research_xml": "RP_Cannons.xml",
        "sound_xml": "Cannons.xml",
        "texture_prefixes": [
            "M57mmDeacon", "U57mmDeacon", "M75mmPak40", "M88mmFlak41",
            "M90mmM26", "M100mmBS3", "M100mmT54", "M105mmLeo1A5",
            "M106mmM40", "M128mmPak44"
        ],
        "weapon_defnames": [
            "Turret_57mmDeacon_Weapon", "Turret_U57mmDeacon_Weapon",
            "Turret_75mmPak40_Weapon", "Turret_88mmFlak41_Weapon",
            "Turret_90mmM26_Weapon", "Turret_100mmBS3_Weapon",
            "Turret_100mmT54_Weapon", "Turret_105mmLeo1A5_Weapon",
            "Turret_106mmM40_Weapon", "Turret_128mmPak44_Weapon"
        ],
        "autoloader_ammo_sets": {
            "AMC_Autoloader_Medium": [
                "AmmoSet_57mmOQF_Shells", "AmmoSet_75mmPAK40_Shells",
                "AmmoSet_88mmFLAK41_Shells", "AmmoSet_90mmM26_Shells",
                "AmmoSet_100mmBS3_Shells", "AmmoSet_105mmL7A3_Shells",
                "AmmoSet_106mmM40_Shells"
            ],
            "AMC_Autoloader_Large": [
                "AmmoSet_128mmPAK44_Shells"
            ]
        }
    },
    "Howitzers": {
        "dir_name": "Absolutely More Cannons - Howitzers",
        "package_id": "faryzal2020.amc.howitzers",
        "display_name": "Absolutely More Cannons - Howitzers",
        "description": "Howitzers module for Absolutely More Cannons CE. Requires AMC Core.",
        "buildings_dir": "Howitzers",
        "unmanned_xmls": ["U152mmMsta.xml"],
        "ammo_xmls": [
            "75mmLEIG18.xml", "122mm2A12.xml", "150mmSIG33.xml", "152mm2A65.xml",
            "155mmGCT.xml", "233mmBLMK1.xml", "406mm2A3.xml", "600mmKarlGerat.xml"
        ],
        "research_xml": "RP_Howitzers.xml",
        "sound_xml": "Howitzers.xml",
        "texture_prefixes": [
            "M75mmLEIG18", "M122mmGvozdika", "M150mmSIG33", "M152mmAkatsiya",
            "M152mmMsta", "U152mmMsta", "M155mmGCT", "M233mmBLMK1",
            "M406mm2A3", "M600mmKarlGerat"
        ],
        "weapon_defnames": [
            "Turret_75mmLEIG18_Weapon", "Turret_122mmGvozdika_Weapon",
            "Turret_150mmSIG33_Weapon", "Turret_152mmAkatsiya_Weapon",
            "Turret_152mmMsta_Weapon", "Turret_152mmMsta_indirect_Weapon",
            "Turret_155mmGCT_Weapon", "Turret_233mmBLMK1_Weapon",
            "Turret_600mmKarlGerat_Weapon"
        ],
        "autoloader_ammo_sets": {
            "AMC_Autoloader_Medium": [
                "AmmoSet_75mmLEIG18_Shells"
            ],
            "AMC_Autoloader_Large": [
                "AmmoSet_122mm2A12_Shells", "AmmoSet_122mm2A12_indirect_Shells",
                "AmmoSet_150mmSIG33_Shells", "AmmoSet_152mm2A65_Shells",
                "AmmoSet_152mm2A65_indirect_Shells", "AmmoSet_155mmGCT_Shells",
                "AmmoSet_155mmGCT_indirect_Shells", "AmmoSet_233mmBLMK1_Shells",
                "AmmoSet_406mm2A3_indirect_Shells", "AmmoSet_600mmKarlGerat_Shells"
            ]
        }
    },
    "Naval Guns": {
        "dir_name": "Absolutely More Cannons - Naval Guns",
        "package_id": "faryzal2020.amc.navalguns",
        "display_name": "Absolutely More Cannons - Naval Guns",
        "description": "Naval Gun Turrets module for Absolutely More Cannons CE. Requires AMC Core.",
        "buildings_dir": "Naval Guns",
        "unmanned_xmls": ["U127mmMark16.xml"],
        "ammo_xmls": [
            "76mmOtomelaraC.xml", "100mmModel1953.xml", "120mmTAK120.xml",
            "127mmMark16.xml", "127mmSKC34.xml", "127mmTypeC.xml",
            "150mmKC36.xml", "203mmSKC34.xml"
        ],
        "research_xml": "RP_NavalGuns.xml",
        "sound_xml": "NavalGuns.xml",
        "texture_prefixes": [
            "M76mmOtomelaraC", "M100mmModel1953", "M120mmTAK120", "M127mmMark16",
            "U127mmMark16", "M127mmOtobredaC", "M127mmSKC34", "M127mmTypeC",
            "M150mmKC36", "M150mmKC36T", "M203mmSKC34"
        ],
        "weapon_defnames": [
            "Turret_127mmSKC34_Weapon", "Turret_127mmMark16_Weapon",
            "Turret_127mmMark16_indirect_Weapon", "Turret_120mmTAK120_Weapon",
            "Turret_120mmTAK120_indirect_Weapon", "Turret_127mmTypeC_Weapon",
            "Turret_127mmOtobredaC_Weapon", "Turret_127mmOtobredaC_indirect_Weapon",
            "Turret_150mmKC36_Weapon", "Turret_150mmKC36T_Weapon",
            "Turret_203mmSKC34_Weapon", "Turret_100mmModel1953_Weapon",
            "Turret_100mmModel1953_indirect_Weapon", "Turret_76mmOtomelaraC_Weapon"
        ],
        "autoloader_ammo_sets": {
            "AMC_Autoloader_Medium": [
                "AmmoSet_76mmOtomelaraC_Shells", "AmmoSet_76mmOtomelaraC_indirect_Shells",
                "AmmoSet_100mmModel1953_Shells", "AmmoSet_100mmModel1953_indirect_Shells"
            ],
            "AMC_Autoloader_Large": [
                "AmmoSet_120mmTAK120_Shells", "AmmoSet_120mmTAK120_indirect_Shells",
                "AmmoSet_127mmMark16_Shells", "AmmoSet_127mmMark16_indirect_Shells",
                "AmmoSet_127mmSKC34_Shells", "AmmoSet_127mmSKC34_indirect_Shells",
                "AmmoSet_127mmTypeC_Shells", "AmmoSet_127mmTypeC_indirect_Shells",
                "AmmoSet_150mmKC36_Shells", "AmmoSet_203mmSKC34_Shells"
            ]
        }
    },
    "Rotary Cannons": {
        "dir_name": "Absolutely More Cannons - Rotary Cannons",
        "package_id": "faryzal2020.amc.rotarycannons",
        "display_name": "Absolutely More Cannons - Rotary Cannons",
        "description": "High rate of fire rotary cannons module for Absolutely More Cannons CE. Requires AMC Core.",
        "buildings_dir": "RotaryCannons",
        "unmanned_xmls": [],
        "ammo_xmls": ["20x102mmVulcan.xml"],
        "research_xml": "RP_RotaryCannons.xml",
        "sound_xml": "RotaryCannons.xml",
        "texture_prefixes": ["M20mmM61Vulcan", "M30mmGAU8Avenger"],
        "weapon_defnames": ["Turret_20mmM61Vulcan_Weapon"],
        "autoloader_ammo_sets": {
            "AMC_Autoloader_Small": [
                "AmmoSet_20x102mmNATO"
            ]
        }
    }
}

def prepare_clean_dir(target_dir):
    """
    Cleans target_dir while preserving metadata files in About/ 
    (such as PublishedFileId.txt, Manifest.xml, etc.) except for About.xml.
    """
    preserved_files = {}
    about_dir = os.path.join(target_dir, "About")
    
    if os.path.isdir(about_dir):
        for fname in os.listdir(about_dir):
            if fname.lower() == "about.xml":
                continue
            fpath = os.path.join(about_dir, fname)
            if os.path.isfile(fpath):
                try:
                    with open(fpath, "rb") as f:
                        preserved_files[fname] = f.read()
                except Exception as e:
                    print(f"   [Warning] Could not read {fname} for preservation: {e}")
                    
    if os.path.exists(target_dir):
        for root, dirs, files in os.walk(target_dir, topdown=False):
            for file in files:
                fpath = os.path.join(root, file)
                try:
                    os.remove(fpath)
                except Exception:
                    pass
            for d in dirs:
                dpath = os.path.join(root, d)
                try:
                    os.rmdir(dpath)
                except Exception:
                    pass
    os.makedirs(target_dir, exist_ok=True)
    
    return preserved_files

def restore_preserved_files(target_dir, preserved_files):
    """Restores preserved About/ files back to target_dir/About."""
    if not preserved_files:
        return
    about_dir = os.path.join(target_dir, "About")
    os.makedirs(about_dir, exist_ok=True)
    for fname, data in preserved_files.items():
        fpath = os.path.join(about_dir, fname)
        try:
            with open(fpath, "wb") as f:
                f.write(data)
            print(f"   [Preserved] Restored {fname} in {os.path.basename(target_dir)}/About/")
        except Exception:
            pass

def copy_file_safe(src, dst):
    """Copy file safely ensuring destination directory exists."""
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    try:
        shutil.copy2(src, dst)
    except PermissionError:
        print(f"   [Notice] Skipping locked file: {dst}")

def generate_core_about(dest_dir):
    """Generate About.xml for Core mod."""
    about_content = """<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>Absolutely More Cannons - Core</name>
  <author>Faryzal2020</author>
  <url>https://github.com/Faryzal2020/AbsolutelyMoreCannonsCE</url>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description>
    Core Framework for Absolutely More Cannons CE. Contains shared C# assemblies, base building definitions, CNC machine, autoloader, and shared UI assets.
  </description>
  <packageId>faryzal2020.amc.core</packageId>
  <modDependencies>
    <li>
      <packageId>CETeam.CombatExtended</packageId>
      <displayName>Combat Extended</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/1631756268</steamWorkshopUrl>
      <downloadUrl>https://github.com/CombatExtended-Continued/CombatExtended/releases/latest</downloadUrl>
    </li>
  </modDependencies>
  <incompatibleWith>
    <li>Faryzal2020.AMC</li>
    <li>Faryzal2020.AMCCE</li>
    <li>Faryzal2020.AMCCE_copy</li>
  </incompatibleWith>
  <loadAfter>
    <li>CETeam.CombatExtended</li>
    <li>PrivateGER.Nukes.Shells_copy</li>
    <li>PrivateGER.Nukes.Shells</li>
    <li>IssacZhuang.MuzzleFlash_copy</li>
    <li>IssacZhuang.MuzzleFlash</li>
  </loadAfter>
</ModMetaData>
"""
    about_path = os.path.join(dest_dir, "About", "About.xml")
    os.makedirs(os.path.dirname(about_path), exist_ok=True)
    with open(about_path, "w", encoding="utf-8") as f:
        f.write(about_content.strip() + "\n")

def generate_module_about(dest_dir, package_id, display_name, description):
    """Generate About.xml for a Module mod."""
    about_content = f"""<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>{display_name}</name>
  <author>Faryzal2020</author>
  <url>https://github.com/Faryzal2020/AbsolutelyMoreCannonsCE</url>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description>
    {description}
  </description>
  <packageId>{package_id}</packageId>
  <modDependencies>
    <li>
      <packageId>faryzal2020.amc.core</packageId>
      <displayName>Absolutely More Cannons - Core</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/3788675677</steamWorkshopUrl>
    </li>
  </modDependencies>
  <incompatibleWith>
    <li>Faryzal2020.AMC</li>
    <li>Faryzal2020.AMCCE</li>
    <li>Faryzal2020.AMCCE_copy</li>
  </incompatibleWith>
  <loadAfter>
    <li>faryzal2020.amc.core</li>
  </loadAfter>
</ModMetaData>
"""
    about_path = os.path.join(dest_dir, "About", "About.xml")
    os.makedirs(os.path.dirname(about_path), exist_ok=True)
    with open(about_path, "w", encoding="utf-8") as f:
        f.write(about_content.strip() + "\n")

def split_muzzle_flash_patches(module_key, weapon_defnames, dest_dir):
    """Extract MuzzleFlash patch operations for specific weapon defNames preserving full XML block structure."""
    src_patch = os.path.join(DEV_ROOT, "Patches", "Muzzle Flash.xml")
    if not os.path.exists(src_patch):
        return

    with open(src_patch, "r", encoding="utf-8") as f:
        content = f.read()

    matched_blocks = []
    lines = content.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if '<li Class="PatchOperationAddModExtension">' in line:
            block_lines = [line]
            depth = 1
            i += 1
            while i < len(lines) and depth > 0:
                cur_line = lines[i]
                block_lines.append(cur_line)
                opens = cur_line.count("<li")
                closes = cur_line.count("</li>")
                depth += (opens - closes)
                i += 1
            block_str = "\n".join(block_lines)
            for defname in weapon_defnames:
                if f'defName="{defname}"' in block_str:
                    matched_blocks.append(block_str)
                    break
        else:
            i += 1

    if matched_blocks:
        patch_xml = f"""<?xml version="1.0" encoding="utf-8"?>
<Patch>
	<Operation Class="PatchOperationFindMod">
		<mods>
			<li>Muzzle Flash</li>
		</mods>
		<match Class="PatchOperationSequence">
			<operations>
{chr(10).join(matched_blocks)}
			</operations>
		</match>
	</Operation>
</Patch>
"""
        patch_dest = os.path.join(dest_dir, "Patches", f"MuzzleFlash_{module_key.replace(' ', '')}.xml")
        os.makedirs(os.path.dirname(patch_dest), exist_ok=True)
        with open(patch_dest, "w", encoding="utf-8") as f:
            f.write(patch_xml.strip() + "\n")

def generate_autoloader_patch(dest_dir, ammo_sets_dict):
    """Generate Patches/Autoloader_Patch.xml for ammo sets."""
    if not ammo_sets_dict:
        return
    operations = []
    for autoloader_def, ammo_sets in ammo_sets_dict.items():
        if not ammo_sets:
            continue
        items_xml = "\n".join(f"\t\t\t<li>{s}</li>" for s in ammo_sets)
        op = f"""\t<Operation Class="PatchOperationAdd">
\t\t<xpath>Defs/ThingDef[defName="{autoloader_def}"]/comps/li[@Class="CombatExtended.CompProperties_AmmoListUser"]/additionalAmmoSets</xpath>
\t\t<value>
{items_xml}
\t\t</value>
\t</Operation>"""
        operations.append(op)
    
    if not operations:
        return
        
    patch_xml = f"""<?xml version="1.0" encoding="utf-8"?>
<Patch>
{chr(10).join(operations)}
</Patch>
"""
    patch_path = os.path.join(dest_dir, "Patches", "Autoloader_Patch.xml")
    os.makedirs(os.path.dirname(patch_path), exist_ok=True)
    with open(patch_path, "w", encoding="utf-8") as f:
        f.write(patch_xml.strip() + "\n")

def deploy_core():
    """Build and export Core mod."""
    dest_dir = os.path.join(MODS_DIR, "Absolutely More Cannons - Core")
    print(f"--> Packaging [Core] into: {dest_dir}")
    preserved = prepare_clean_dir(dest_dir)

    # 1. About.xml
    generate_core_about(dest_dir)

    # 2. Assemblies
    src_assemblies = os.path.join(DEV_ROOT, "Assemblies")
    if os.path.exists(src_assemblies):
        shutil.copytree(src_assemblies, os.path.join(dest_dir, "Assemblies"), dirs_exist_ok=True, copy_function=copy_file_safe)

    # 3. Base Buildings
    base_buildings = ["TurretBuilding_Base.xml", "Autoloader.xml", "CNCmachine.xml", "Materials.xml"]
    for b in base_buildings:
        src = os.path.join(DEV_ROOT, "Common", "Defs", "ThingDefs_Buildings", b)
        if os.path.exists(src):
            copy_file_safe(src, os.path.join(dest_dir, "Common", "Defs", "ThingDefs_Buildings", b))

    # 4. Items (FCS)
    src_items = os.path.join(DEV_ROOT, "Common", "Defs", "ThingDefs_Items", "Items_FCS.xml")
    if os.path.exists(src_items):
        copy_file_safe(src_items, os.path.join(dest_dir, "Common", "Defs", "ThingDefs_Items", "Items_FCS.xml"))

    # 5. Core Ammo
    core_ammo = ["AmmoCategoryDefs.xml", "Projectiles_Fragments.xml"]
    for a in core_ammo:
        src = os.path.join(DEV_ROOT, "Common", "Defs", "Ammo", a)
        if os.path.exists(src):
            copy_file_safe(src, os.path.join(dest_dir, "Common", "Defs", "Ammo", a))

    # 6. Shared Def Directories
    shared_dirs = ["Designations", "Effects", "JobDefs", "ThingCategoryDefs"]
    for sd in shared_dirs:
        src_d = os.path.join(DEV_ROOT, "Common", "Defs", sd)
        if os.path.exists(src_d):
            shutil.copytree(src_d, os.path.join(dest_dir, "Common", "Defs", sd), dirs_exist_ok=True, copy_function=copy_file_safe)

    # 7. Core Research
    core_research = ["ResearchTab.xml", "RP_AMC.xml"]
    for r in core_research:
        src = os.path.join(DEV_ROOT, "Common", "Defs", "ResearchProjectDef", r)
        if os.path.exists(src):
            copy_file_safe(src, os.path.join(dest_dir, "Common", "Defs", "ResearchProjectDef", r))

    # 8. Core Sounds
    src_snd_def = os.path.join(DEV_ROOT, "Common", "Defs", "SoundDefs", "Others.xml")
    if os.path.exists(src_snd_def):
        copy_file_safe(src_snd_def, os.path.join(dest_dir, "Common", "Defs", "SoundDefs", "Others.xml"))

    src_sounds = os.path.join(DEV_ROOT, "Common", "Sounds")
    if os.path.exists(src_sounds):
        shutil.copytree(src_sounds, os.path.join(dest_dir, "Common", "Sounds"), dirs_exist_ok=True, copy_function=copy_file_safe)

    # 9. Core Textures (UI, Effects, Items, Motes, Pawns, Projectiles, Shared Icons, Shared Buildings)
    src_ui_tex = os.path.join(DEV_ROOT, "Common", "Textures", "UI")
    if os.path.exists(src_ui_tex):
        shutil.copytree(src_ui_tex, os.path.join(dest_dir, "Common", "Textures", "UI"), dirs_exist_ok=True, copy_function=copy_file_safe)

    src_effects_tex = os.path.join(DEV_ROOT, "Common", "Textures", "Effects")
    if os.path.exists(src_effects_tex):
        shutil.copytree(src_effects_tex, os.path.join(dest_dir, "Common", "Textures", "Effects"), dirs_exist_ok=True, copy_function=copy_file_safe)

    for sub in ["Item", "Mote", "Pawn", "Projectile"]:
        src_sub = os.path.join(DEV_ROOT, "Common", "Textures", "Things", sub)
        if os.path.exists(src_sub):
            shutil.copytree(src_sub, os.path.join(dest_dir, "Common", "Textures", "Things", sub), dirs_exist_ok=True, copy_function=copy_file_safe)

    icons_dir = os.path.join(DEV_ROOT, "Common", "Textures", "Things", "Icons")
    if os.path.exists(icons_dir):
        for icon in ["MenuIcon.png", "MenuIcon.dds", "Materials_CategoryIcon.png", "Shells_CategoryIcon.png", "Shells_CategoryIcon.dds", "FCS_Computer.png", "FCS_Computer.dds"]:
            src = os.path.join(icons_dir, icon)
            if os.path.exists(src):
                copy_file_safe(src, os.path.join(dest_dir, "Common", "Textures", "Things", "Icons", icon))

    shared_bg_files = ["5x5_Base.png", "6x6_Base.png", "cncmachine_east.png", "cncmachine_north.png",
                       "cncmachine_south.png", "cncmodule_east.png", "cncmodule_north.png", "cncmodule_south.png"]
    for sbg in shared_bg_files:
        src = os.path.join(DEV_ROOT, "Common", "Textures", "Things", "Building", sbg)
        if os.path.exists(src):
            copy_file_safe(src, os.path.join(dest_dir, "Common", "Textures", "Things", "Building", sbg))

    src_autoloader_tex = os.path.join(DEV_ROOT, "Common", "Textures", "Things", "Building", "Autoloader")
    if os.path.exists(src_autoloader_tex):
        shutil.copytree(src_autoloader_tex, os.path.join(dest_dir, "Common", "Textures", "Things", "Building", "Autoloader"), dirs_exist_ok=True, copy_function=copy_file_safe)

    # Restore preserved publisher metadata (PublishedFileId.txt, Manifest.xml, etc.)
    restore_preserved_files(dest_dir, preserved)
    print("   [OK] Core packaged successfully.\n")

def deploy_modules():
    """Build and export 5 category modules."""
    for key, spec in MODULES_SPEC.items():
        dest_dir = os.path.join(MODS_DIR, spec["dir_name"])
        print(f"--> Packaging [{key}] into: {dest_dir}")
        preserved = prepare_clean_dir(dest_dir)

        # 1. About.xml
        generate_module_about(dest_dir, spec["package_id"], spec["display_name"], spec["description"])

        # 2. Buildings (Manned folder + Unmanned files)
        src_b_folder = os.path.join(DEV_ROOT, "Common", "Defs", "ThingDefs_Buildings", spec["buildings_dir"])
        if os.path.exists(src_b_folder):
            shutil.copytree(src_b_folder, os.path.join(dest_dir, "Common", "Defs", "ThingDefs_Buildings", spec["buildings_dir"]), dirs_exist_ok=True, copy_function=copy_file_safe)

        for unm in spec["unmanned_xmls"]:
            src_u = os.path.join(DEV_ROOT, "Common", "Defs", "ThingDefs_Buildings", "Unmanned", unm)
            if os.path.exists(src_u):
                copy_file_safe(src_u, os.path.join(dest_dir, "Common", "Defs", "ThingDefs_Buildings", spec["buildings_dir"], unm))

        # 3. Ammo
        for ammo in spec["ammo_xmls"]:
            src_a = os.path.join(DEV_ROOT, "Common", "Defs", "Ammo", ammo)
            if os.path.exists(src_a):
                copy_file_safe(src_a, os.path.join(dest_dir, "Common", "Defs", "Ammo", ammo))

        # 4. Research
        src_r = os.path.join(DEV_ROOT, "Common", "Defs", "ResearchProjectDef", spec["research_xml"])
        if os.path.exists(src_r):
            copy_file_safe(src_r, os.path.join(dest_dir, "Common", "Defs", "ResearchProjectDef", spec["research_xml"]))

        # 5. Sounds
        src_s = os.path.join(DEV_ROOT, "Common", "Defs", "SoundDefs", spec["sound_xml"])
        if os.path.exists(src_s):
            copy_file_safe(src_s, os.path.join(dest_dir, "Common", "Defs", "SoundDefs", spec["sound_xml"]))

        # 6. Textures (Building & Icons)
        for category in ["Building", "Icons"]:
            cat_dir = os.path.join(DEV_ROOT, "Common", "Textures", "Things", category)
            if os.path.exists(cat_dir):
                for item in os.listdir(cat_dir):
                    item_path = os.path.join(cat_dir, item)
                    for prefix in spec["texture_prefixes"]:
                        if item.startswith(prefix):
                            dest_path = os.path.join(dest_dir, "Common", "Textures", "Things", category, item)
                            if os.path.isdir(item_path):
                                shutil.copytree(item_path, dest_path, dirs_exist_ok=True, copy_function=copy_file_safe)
                            else:
                                copy_file_safe(item_path, dest_path)

        # 7. MuzzleFlash Patches
        split_muzzle_flash_patches(key, spec["weapon_defnames"], dest_dir)

        # 8. Autoloader Ammo Set Patches
        generate_autoloader_patch(dest_dir, spec.get("autoloader_ammo_sets"))

        # Restore preserved publisher metadata (PublishedFileId.txt, Manifest.xml, etc.)
        restore_preserved_files(dest_dir, preserved)
        print(f"   [OK] {key} packaged successfully.\n")

def deploy_aio():
    """Build and export Complete AiO mod."""
    dest_dir = os.path.join(MODS_DIR, "Absolutely More Cannons - CE")
    print(f"--> Packaging [Complete AiO] into: {dest_dir}")
    preserved = prepare_clean_dir(dest_dir)

    # Ignore list for production release
    def ignore_patterns(path, names):
        ignored = []
        for name in names:
            if name.endswith(".bak") or name.endswith(".cs"):
                ignored.append(name)
            elif name in [".git", ".gitignore", ".agent", ".agents", ".gemini", "DevTools", "TurretEditor", "docs", "scratch", "Source"]:
                ignored.append(name)
        return ignored

    for item in os.listdir(DEV_ROOT):
        if item in [".git", ".gitignore", ".agent", ".agents", ".gemini", "DevTools", "TurretEditor", "docs", "scratch", "Source"]:
            continue
        src_p = os.path.join(DEV_ROOT, item)
        dest_p = os.path.join(dest_dir, item)
        if os.path.isdir(src_p):
            shutil.copytree(src_p, dest_p, ignore=ignore_patterns, dirs_exist_ok=True, copy_function=copy_file_safe)
        else:
            if not (item.endswith(".bak") or item.endswith(".cs")):
                copy_file_safe(src_p, dest_p)

    # Generate merged Autoloader Patches for AiO
    merged_autoloader_ammo = {}
    for spec in MODULES_SPEC.values():
        for al_def, ammo_sets in spec.get("autoloader_ammo_sets", {}).items():
            if al_def not in merged_autoloader_ammo:
                merged_autoloader_ammo[al_def] = []
            merged_autoloader_ammo[al_def].extend(ammo_sets)
    generate_autoloader_patch(dest_dir, merged_autoloader_ammo)

    # Post-process About.xml for production published AiO
    aio_about_path = os.path.join(dest_dir, "About", "About.xml")
    if os.path.exists(aio_about_path):
        with open(aio_about_path, "r", encoding="utf-8") as f:
            content = f.read()
        # Set release packageId without _copy
        content = content.replace("<packageId>Faryzal2020.AMCCE_copy</packageId>", "<packageId>Faryzal2020.AMCCE</packageId>")
        # Ensure incompatibleWith includes dev copy and core module
        incompatible_block = """  <incompatibleWith>
    <li>Faryzal2020.AMC</li>
    <li>Faryzal2020.AMCCE_copy</li>
    <li>faryzal2020.amc.core</li>
  </incompatibleWith>"""
        if "<incompatibleWith>" in content:
            # Replace existing block
            import re
            content = re.sub(r'<incompatibleWith>.*?</incompatibleWith>', incompatible_block, content, flags=re.DOTALL)
        with open(aio_about_path, "w", encoding="utf-8") as f:
            f.write(content)

    # Restore preserved publisher metadata (PublishedFileId.txt, Manifest.xml, etc.)
    restore_preserved_files(dest_dir, preserved)
    print("   [OK] Complete AiO packaged successfully.\n")

def main():
    print("==================================================")
    print("  Starting Absolutely More Cannons Deploy Pipeline")
    print("==================================================\n")
    deploy_core()
    deploy_modules()
    deploy_aio()
    print("==================================================")
    print("  Deploy Pipeline Finished Successfully!")
    print("==================================================")

if __name__ == "__main__":
    main()
