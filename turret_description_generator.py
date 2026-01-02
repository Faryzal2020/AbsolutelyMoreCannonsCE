#!/usr/bin/env python3
"""
Turret Description Generator for Absolutely More Cannons
Automatically generates and injects turret descriptions based on XML data.
"""

import xml.etree.ElementTree as ET
import json
import os
import argparse
import re
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass, field


@dataclass
class AmmoStats:
    """Statistics for a single ammo type"""
    ammo_label: str = ""
    base_damage: float = 0
    explosion_radius: float = 0
    armor_penetration: float = 0
    shelling_damage: float = 0  # For settlement bombardment


@dataclass
class TurretStats:
    """Complete statistics for a turret"""
    def_name: str
    label: str
    parent_name: str = ""
    
    # Basic stats
    work_to_build: int = 0
    max_hit_points: int = 0
    power_consumption: int = 0
    
    # Weapon stats
    min_range: float = 0
    max_range: float = 0
    cooldown: float = 0
    burst_count: int = 0
    magazine_size: int = 0
    reload_time: float = 0
    
    # Optional parameters
    max_rpms: List[int] = field(default_factory=list)
    selectable_bursts: List[int] = field(default_factory=list)
    is_ciws: bool = False
    
    # Ammo and mode
    ammo_set: str = ""
    ammo_stats: Dict[str, AmmoStats] = field(default_factory=dict)
    alternate_def: str = ""  # For mode-swap turrets
    shelling_range: int = 0  # World tile range for settlement bombardment
    
    # Metadata
    is_manned: bool = True
    is_indirect: bool = False
    thing_class: str = ""


class TurretDescriptionGenerator:
    def __init__(self, mod_path: str, test_mode: bool = False):
        self.mod_path = Path(mod_path)
        self.test_mode = test_mode
        self.test_turrets = [
            "Turret_120mmTAK120_Base", 
            "Turret_120mmTAK120_indirect_Base",
            "Turret_88mmFlak41_Base",
            "Turret_152mmMsta_Base",
            "Turret_152mmMsta_indirect_Base",
            "Turret_20mmM61Vulcan_Base",
            "Turret_30mmKugelblitz_Base",
            "Turret_57mmDeacon_Base"
        ]
        
        self.turret_defs_path = self.mod_path / "Common" / "Defs" / "ThingDefs_Buildings"
        self.ammo_defs_path = self.mod_path / "Common" / "Defs" / "Ammo"
        
        self.turrets: Dict[str, TurretStats] = {}
        self.ammo_sets: Dict[str, Dict[str, str]] = {}  # ammoSet -> {ammo_name: bullet_name}
        self.flavor_texts: Dict[str, str] = {}
    
    def find_xml_files(self, directory: Path) -> List[Path]:
        """Recursively find all XML files in directory"""
        return list(directory.rglob("*.xml"))
    
    def get_xml_text(self, element: Optional[ET.Element], default: str = "") -> str:
        """Safely get text from XML element"""
        if element is not None and element.text:
            return element.text.strip()
        return default
    
    def get_xml_float(self, element: Optional[ET.Element], default: float = 0) -> float:
        """Safely get float from XML element"""
        text = self.get_xml_text(element)
        try:
            return float(text) if text else default
        except ValueError:
            return default
    
    def get_xml_int(self, element: Optional[ET.Element], default: int = 0) -> int:
        """Safely get int from XML element"""
        text = self.get_xml_text(element)
        try:
            return int(text) if text else default
        except ValueError:
            return default
    
    def is_turret_parent(self, parent_name: str) -> bool:
        """Check if ParentName indicates a valid turret"""
        turret_parents = ["AMCTurretMannedBase", "AMCTurretAutoBase", "AMCArtilleryBase"]
        return any(p in parent_name for p in turret_parents)
    
    def is_manned_turret(self, parent_name: str) -> bool:
        """Determine if turret is manned based on ParentName"""
        return "Manned" in parent_name
    
    def is_ciws_turret(self, thing_class: str, weapon_root: Optional[ET.Element]) -> bool:
        """Check if turret has CIWS capability"""
        # Check thingClass
        if "CIWS" in thing_class:
            return True
        
        # Check weapon verbs
        if weapon_root is not None:
            verbs = weapon_root.find(".//verbs")
            if verbs is not None:
                for verb in verbs.findall("li"):
                    verb_class = verb.get("Class", "")
                    if "CIWS" in verb_class:
                        return True
        return False
    
    def parse_turret_xml(self, xml_path: Path) -> None:
        """Parse a single turret XML file"""
        try:
            tree = ET.parse(xml_path)
            root = tree.getroot()
            
            # Find all ThingDef elements for turret buildings
            for thing_def in root.findall(".//ThingDef"):
                parent_name = thing_def.get("ParentName", "")
                
                # Skip if not a turret parent
                if not self.is_turret_parent(parent_name):
                    continue
                
                def_name = self.get_xml_text(thing_def.find("defName"))
                
                # Skip if not in test list (when in test mode)
                if self.test_mode and def_name not in self.test_turrets:
                    continue
                
                # Initialize turret stats
                turret = TurretStats(
                    def_name=def_name,
                    label=self.get_xml_text(thing_def.find("label")),
                    parent_name=parent_name,
                    is_manned=self.is_manned_turret(parent_name),
                    is_indirect="Artillery" in parent_name or "indirect" in def_name.lower(),
                    thing_class=self.get_xml_text(thing_def.find("thingClass"))
                )
                
                # Extract basic stats
                stat_bases = thing_def.find("statBases")
                if stat_bases is not None:
                    turret.max_hit_points = self.get_xml_int(stat_bases.find("MaxHitPoints"))
                    turret.work_to_build = self.get_xml_int(stat_bases.find("WorkToBuild"))
                
                # Extract power consumption
                comps = thing_def.find("comps")
                if comps is not None:
                    for comp in comps.findall("li"):
                        if comp.get("Class") == "CompProperties_Power":
                            turret.power_consumption = self.get_xml_int(comp.find("basePowerConsumption"))
                        
                        # Check for mode swap
                        if "TurretModeSwap" in comp.get("Class", ""):
                            turret.alternate_def = self.get_xml_text(comp.find("alternateDef"))
                
                # Find and parse weapon definition
                building = thing_def.find("building")
                if building is not None:
                    weapon_def_name = self.get_xml_text(building.find("turretGunDef"))
                    if weapon_def_name:
                        self.parse_weapon_stats(turret, xml_path, weapon_def_name)
                
                # Extract optional parameters from modExtensions
                mod_extensions = thing_def.find("modExtensions")
                if mod_extensions is not None:
                    barrel_ext = mod_extensions.find(".//li[@Class='AbsolutelyMoreCannons.TurretBarrelExtension']")
                    if barrel_ext is not None:
                        # Extract maxRPMs
                        max_rpms = barrel_ext.find("maxRPMs")
                        if max_rpms is not None:
                            turret.max_rpms = [self.get_xml_int(li) for li in max_rpms.findall("li")]
                        
                        # Extract selectableBurstCounts
                        burst_counts = barrel_ext.find("selectableBurstCounts")
                        if burst_counts is not None:
                            turret.selectable_bursts = [self.get_xml_int(li) for li in burst_counts.findall("li")]
                
                # Check CIWS capability
                turret.is_ciws = self.is_ciws_turret(turret.thing_class, None)
                
                self.turrets[def_name] = turret
                print(f"  Parsed turret: {def_name}")
                
        except ET.ParseError as e:
            print(f"  Error parsing {xml_path}: {e}")
    
    def parse_weapon_stats(self, turret: TurretStats, xml_path: Path, weapon_def_name: str) -> None:
        """Parse weapon stats from the same XML file"""
        try:
            tree = ET.parse(xml_path)
            root = tree.getroot()
            
            # Find weapon ThingDef
            for thing_def in root.findall(".//ThingDef"):
                def_name = self.get_xml_text(thing_def.find("defName"))
                if def_name == weapon_def_name:
                    # Extract stat bases
                    stat_bases = thing_def.find("statBases")
                    if stat_bases is not None:
                        turret.cooldown = self.get_xml_float(stat_bases.find("RangedWeapon_Cooldown"))
                    
                    # Extract verb properties
                    verbs = thing_def.find("verbs")
                    if verbs is not None:
                        for verb in verbs.findall("li"):
                            # Only process the main verb (not CIWS verbs)
                            verb_class = verb.get("Class", "")
                            if "CIWS" not in verb_class:
                                turret.min_range = self.get_xml_float(verb.find("minRange"))
                                turret.max_range = self.get_xml_float(verb.find("range"))
                                turret.burst_count = self.get_xml_int(verb.find("burstShotCount"))
                                break
                        
                        # Check for CIWS verbs
                        for verb in verbs.findall("li"):
                            verb_class = verb.get("Class", "")
                            if "CIWS" in verb_class:
                                turret.is_ciws = True
                                break
                    
                    # Extract ammo user properties
                    comps = thing_def.find("comps")
                    if comps is not None:
                        for comp in comps.findall("li"):
                            if "AmmoUser" in comp.get("Class", ""):
                                turret.magazine_size = self.get_xml_int(comp.find("magazineSize"))
                                turret.reload_time = self.get_xml_float(comp.find("reloadTime"))
                                turret.ammo_set = self.get_xml_text(comp.find("ammoSet"))
                    
                    break
        except ET.ParseError as e:
            print(f"  Error parsing weapon from {xml_path}: {e}")
    
    def parse_ammo_sets(self) -> None:
        """Parse all ammo set definitions"""
        print("\nParsing ammo sets...")
        ammo_files = self.find_xml_files(self.ammo_defs_path)
        
        for ammo_file in ammo_files:
            try:
                tree = ET.parse(ammo_file)
                root = tree.getroot()
                
                # Find AmmoSetDef elements - try both with and without namespace
                ammo_set_defs = root.findall(".//CombatExtended.AmmoSetDef")
                if not ammo_set_defs:
                    # Try with wildcard namespace
                    ammo_set_defs = root.findall(".//{*}AmmoSetDef")
                
                for ammo_set_def in ammo_set_defs:
                    set_name = self.get_xml_text(ammo_set_def.find("defName"))
                    if not set_name:
                        continue
                    
                    ammo_types = ammo_set_def.find("ammoTypes")
                    
                    if ammo_types is not None:
                        ammo_map = {}
                        for ammo_elem in ammo_types:
                            ammo_name = ammo_elem.tag
                            bullet_name = ammo_elem.text.strip() if ammo_elem.text else ""
                            if bullet_name:
                                ammo_map[ammo_name] = bullet_name
                        
                        if ammo_map:
                            self.ammo_sets[set_name] = ammo_map
                            print(f"  Found ammo set: {set_name} with {len(ammo_map)} types")
            
            except ET.ParseError as e:
                print(f"  Error parsing {ammo_file}: {e}")
    
    def parse_ammo_stats(self) -> None:
        """Parse statistics for each ammo type"""
        print("\nParsing ammo statistics...")
        ammo_files = self.find_xml_files(self.ammo_defs_path)
        
        # First pass: collect all projectile stats
        projectile_stats = {}  # bullet_name -> AmmoStats
        shelling_ranges = {}  # parent_name -> shelling_range
        
        for ammo_file in ammo_files:
            try:
                tree = ET.parse(ammo_file)
                root = tree.getroot()
                
                # First, collect shelling ranges from parent defs
                for thing_def in root.findall(".//ThingDef[@Name]"):
                    parent_name = thing_def.get("Name", "")
                    if parent_name:
                        # Try to find projectile element with both formats
                        projectile = thing_def.find(".//projectile[@Class='CombatExtended.ProjectilePropertiesCE']")
                        if projectile is not None:
                            shelling_props = projectile.find("shellingProps")
                            if shelling_props is not None:
                                shelling_range = self.get_xml_int(shelling_props.find("range"))
                                if shelling_range > 0:
                                    shelling_ranges[parent_name] = shelling_range
                
                # Now collect bullet stats
                for thing_def in root.findall(".//ThingDef"):
                    bullet_name = self.get_xml_text(thing_def.find("defName"))
                    if not bullet_name:
                        continue
                    
                    # Extract projectile properties - use Class attribute search
                    projectile = thing_def.find(".//projectile[@Class='CombatExtended.ProjectilePropertiesCE']")
                    if projectile is not None:
                        stats = AmmoStats()
                        stats.ammo_label = self.get_xml_text(thing_def.find("label"))
                        stats.base_damage = self.get_xml_float(projectile.find("damageAmountBase"))
                        stats.explosion_radius = self.get_xml_float(projectile.find("explosionRadius"))
                        stats.armor_penetration = self.get_xml_float(projectile.find("armorPenetrationSharp"))
                        
                        # Extract shelling damage if present
                        shelling_props = projectile.find("shellingProps")
                        if shelling_props is not None:
                            stats.shelling_damage = self.get_xml_float(shelling_props.find("damage"))
                        
                        projectile_stats[bullet_name] = stats
                        
                        # Get shelling range from parent
                        parent_name = thing_def.get("ParentName", "")
                        if parent_name and parent_name in shelling_ranges:
                            # Store range temporarily - we'll apply it to turrets later
                            pass
            
            except ET.ParseError as e:
                print(f"  Error parsing {ammo_file}: {e}")
        
        print(f"  Found {len(projectile_stats)} projectile definitions")
        print(f"  Found {len(shelling_ranges)} shelling range definitions")
        
        # Second pass: map projectiles to turrets via ammo sets
        for turret in self.turrets.values():
            if not turret.ammo_set:
                continue
            
            if turret.ammo_set not in self.ammo_sets:
                continue
            
            ammo_map = self.ammo_sets[turret.ammo_set]
            for ammo_name, bullet_name in ammo_map.items():
                if bullet_name in projectile_stats:
                    turret.ammo_stats[ammo_name] = projectile_stats[bullet_name]
                    
                    # Set shelling range for this turret based on bullet's parent
                    if turret.shelling_range == 0:
                        # Find the bullet definition to get its parent
                        for ammo_file in ammo_files:
                            try:
                                tree = ET.parse(ammo_file)
                                root = tree.getroot()
                                for thing_def in root.findall(f".//ThingDef[defName='{bullet_name}']"):
                                    parent_name = thing_def.get("ParentName", "")
                                    if parent_name in shelling_ranges:
                                        turret.shelling_range = shelling_ranges[parent_name]
                                        break
                                if turret.shelling_range > 0:
                                    break
                            except:
                                pass
        
        # Log results
        for turret_name, turret in self.turrets.items():
            if turret.ammo_stats:
                print(f"  {turret_name}: {len(turret.ammo_stats)} ammo types, shelling range: {turret.shelling_range}")
    
    def load_flavor_texts(self, flavor_file: Path) -> None:
        """Load flavor texts from file"""
        if not flavor_file.exists():
            print(f"Warning: Flavor text file not found: {flavor_file}")
            return
        
        print(f"\nLoading flavor texts from {flavor_file}...")
        with open(flavor_file, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue
                
                # Parse format: Turret_Name: FlavorText: "text here"
                match = re.match(r'([^:]+):\s*FlavorText:\s*"([^"]*)"', line)
                if match:
                    turret_name, flavor = match.groups()
                    self.flavor_texts[turret_name.strip()] = flavor.strip()
        
        print(f"  Loaded {len(self.flavor_texts)} flavor texts")
    
    def generate_flavor_template(self, output_file: Path) -> None:
        """Generate template file for user to fill in flavor texts"""
        print(f"\nGenerating flavor text template: {output_file}")
        
        # First, scan all turrets
        print("Scanning turret definitions...")
        categories = [d for d in self.turret_defs_path.iterdir() if d.is_dir()]
        
        for category in categories:
            print(f"  Scanning {category.name}...")
            xml_files = self.find_xml_files(category)
            for xml_file in xml_files:
                self.parse_turret_xml(xml_file)
        
        # Write template
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write("# Turret Flavor Text Template\n")
            f.write("# Fill in the flavor text for each turret between the quotes\n")
            f.write("# Format: TurretDefName: FlavorText: \"Your flavor text here\"\n\n")
            
            for turret_name in sorted(self.turrets.keys()):
                f.write(f'{turret_name}: FlavorText: ""\n')
        
        print(f"  Template generated with {len(self.turrets)} turrets")
    
    def scan_turrets(self) -> None:
        """Scan all turret XML files"""
        print("Scanning turret definitions...")
        categories = [d for d in self.turret_defs_path.iterdir() if d.is_dir()]
        
        for category in categories:
            print(f"  Scanning {category.name}...")
            xml_files = self.find_xml_files(category)
            for xml_file in xml_files:
                self.parse_turret_xml(xml_file)
        
        print(f"  Found {len(self.turrets)} turrets")
    
    def generate_descriptions(self) -> Dict[str, Dict[str, str]]:
        """Generate descriptions for all turrets"""
        print("\nGenerating descriptions...")
        descriptions = {}
        processed_pairs = set()  # Track turret pairs we've already processed
        
        for turret_name, turret in self.turrets.items():
            # Skip if this turret is part of a pair we've already processed
            if turret_name in processed_pairs:
                continue
            
            desc = self.format_turret_description(turret)
            
            # Add flavor text if it's a dual-mode turret (flavor text goes at the end of combined description)
            if turret.alternate_def and turret.alternate_def in self.turrets:
                # Get the base turret name (the direct version) for flavor text
                base_turret_name = turret.def_name if not turret.is_indirect else turret.alternate_def
                flavor = self.flavor_texts.get(base_turret_name, "")
                if flavor:
                    desc += f" {flavor}"
                
                # Apply the same description to both versions
                descriptions[turret_name] = {"description": desc}
                descriptions[turret.alternate_def] = {"description": desc}
                
                # Mark both as processed
                processed_pairs.add(turret_name)
                processed_pairs.add(turret.alternate_def)
                
                print(f"  Generated (dual-mode): {turret_name} and {turret.alternate_def}")
            else:
                # Single-mode turret (flavor already added in format function)
                descriptions[turret_name] = {"description": desc}
                print(f"  Generated: {turret_name}")
        
        return descriptions
    
    def format_turret_description(self, turret: TurretStats) -> str:
        """Format a single turret description"""
        # Check if this turret has a mode-swap partner
        is_direct_version = turret.alternate_def and not turret.is_indirect
        is_indirect_version = turret.is_indirect and turret.alternate_def
        
        # For dual-mode turrets, find the partner
        partner_turret = None
        if turret.alternate_def and turret.alternate_def in self.turrets:
            partner_turret = self.turrets[turret.alternate_def]
        
        # If this is a dual-mode turret, generate combined description
        if partner_turret:
            # Determine which is direct and which is indirect
            if is_direct_version:
                direct_turret = turret
                indirect_turret = partner_turret
            else:
                # This is the indirect version, swap them
                direct_turret = partner_turret
                indirect_turret = turret
            
            # Generate description for direct mode
            direct_desc = self._format_single_mode_description(direct_turret, "Direct Fire Mode")
            
            # Generate description for indirect mode
            indirect_desc = self._format_single_mode_description(indirect_turret, "Indirect Fire Mode")
            
            # Combine with double newline
            return f"{direct_desc}\n\n{indirect_desc}"
        else:
            # Single-mode turret
            return self._format_single_mode_description(turret, None)
    
    def _format_single_mode_description(self, turret: TurretStats, mode_label: str = None) -> str:
        """Format description for a single mode (or single-mode turret)"""
        parts = []
        
        # Add mode label if provided
        if mode_label:
            parts.append(mode_label)
        
        # For indirect mode, only show Min Range and ammo stats
        if mode_label == "Indirect Fire Mode":
            # Only Min Range
            if turret.min_range > 0:
                parts.append(f"Min Range: {turret.min_range:.0f}")
            
            # Ammunition with settlement bombardment
            if turret.ammo_stats:
                ammo_parts = []
                for ammo_name, stats in turret.ammo_stats.items():
                    ammo_desc = self.format_ammo_description(stats)
                    ammo_parts.append(ammo_desc)
                
                if ammo_parts:
                    parts.append(f"Ammunition: {', '.join(ammo_parts)}")
            
            # Settlement bombardment
            if turret.shelling_range > 0 and turret.ammo_stats:
                shelling_parts = []
                for ammo_name, stats in turret.ammo_stats.items():
                    if stats.shelling_damage > 0:
                        # Extract ammo type (HE, AP, etc.) from name
                        ammo_type = "Unknown"
                        if "HE" in ammo_name.upper():
                            ammo_type = "HE"
                        elif "APHE" in ammo_name.upper():
                            ammo_type = "APHE"
                        elif "AP" in ammo_name.upper():
                            ammo_type = "AP"
                        
                        shelling_parts.append(f"{ammo_type}: {stats.shelling_damage:.2f} dmg")
                
                if shelling_parts:
                    parts.append(f"Settlement bombardment range: {turret.shelling_range} world tiles ({', '.join(shelling_parts)})")
            
            return ", ".join(parts) + "."
        
        # For direct mode or single-mode turrets, show all stats
        # Manned status
        manned_status = "Yes" if turret.is_manned else "No"
        parts.append(f"Manned: {manned_status}")
        
        # CIWS capability
        if turret.is_ciws:
            parts.append("Projectile and Drop Pod Interception: Available")
        
        # Power
        if turret.power_consumption > 0:
            parts.append(f"Power: {turret.power_consumption}W")
        else:
            parts.append("Power: None")
        
        # HP
        parts.append(f"HP: {turret.max_hit_points}")
        
        # Work to build
        if turret.work_to_build > 0:
            parts.append(f"Work: {turret.work_to_build}")
        
        # Cooldown
        parts.append(f"Cooldown: {turret.cooldown}s")
        
        # Range
        if turret.min_range > 0:
            parts.append(f"Min Range: {turret.min_range:.0f}, Max Range: {turret.max_range:.0f}")
        else:
            parts.append(f"Max Range: {turret.max_range:.0f}")
        
        # Rate of fire
        if turret.max_rpms:
            rpm_str = "-".join(map(str, turret.max_rpms))
            parts.append(f"Rate of Fire: {rpm_str} RPM")
        elif turret.cooldown > 0:
            rof = 60 / turret.cooldown
            parts.append(f"Rate of Fire: {rof:.0f} rounds per minute")
        
        # Burst count
        if turret.selectable_bursts:
            burst_str = "-".join(map(str, turret.selectable_bursts))
            parts.append(f"Burst Count: {burst_str}")
        elif turret.burst_count > 0:
            parts.append(f"Burst Count: {turret.burst_count}")
        
        # Magazine and reload
        if turret.magazine_size > 0:
            if turret.magazine_size == 1:
                parts.append("Breech loaded")
            else:
                parts.append(f"Magazine: {turret.magazine_size}")
        if turret.reload_time > 0:
            parts.append(f"Reload: {turret.reload_time}s")
        
        # Ammunition
        if turret.ammo_stats:
            ammo_parts = []
            for ammo_name, stats in turret.ammo_stats.items():
                ammo_desc = self.format_ammo_description(stats)
                ammo_parts.append(ammo_desc)
            
            if ammo_parts:
                parts.append(f"Ammunition: {', '.join(ammo_parts)}")
        
        # Flavor text (only add to single-mode turrets or if no mode label)
        if not mode_label:
            flavor = self.flavor_texts.get(turret.def_name, "")
            description = ", ".join(parts) + "."
            if flavor:
                description += f" {flavor}"
            return description
        else:
            # For mode-labeled descriptions, flavor text will be added at the end of combined description
            return ", ".join(parts) + "."
    
    def format_ammo_description(self, stats: AmmoStats) -> str:
        """Format ammunition statistics"""
        parts = []
        
        # Determine ammo type from stats
        if stats.explosion_radius > 0:
            parts.append(f"HE shell ({stats.explosion_radius}m explosion, base dmg: {stats.base_damage:.0f})")
        elif stats.armor_penetration > 0:
            parts.append(f"AP shell ({stats.armor_penetration:.0f} penetration, base dmg: {stats.base_damage:.0f})")
        else:
            parts.append(f"shell (base dmg: {stats.base_damage:.0f})")
        
        return " ".join(parts)
    
    def save_descriptions(self, descriptions: Dict, output_file: Path) -> None:
        """Save descriptions to JSON file"""
        print(f"\nSaving descriptions to {output_file}...")
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(descriptions, f, indent=2, ensure_ascii=False)
        print(f"  Saved {len(descriptions)} descriptions")
    
    def inject_descriptions(self, descriptions_file: Path) -> None:
        """Inject descriptions from JSON into XML files"""
        print(f"\nLoading descriptions from {descriptions_file}...")
        with open(descriptions_file, 'r', encoding='utf-8') as f:
            descriptions = json.load(f)
        
        print(f"Injecting {len(descriptions)} descriptions into XML files...")
        
        # Scan turret XMLs to find files that need updating
        categories = [d for d in self.turret_defs_path.iterdir() if d.is_dir()]
        
        for category in categories:
            if self.test_mode and category.name not in ["Naval Guns", "Cannons"]:
                continue
            
            print(f"  Processing {category.name}...")
            xml_files = self.find_xml_files(category)
            
            for xml_path in xml_files:
                self.inject_into_xml(xml_path, descriptions)
    
    def inject_into_xml(self, xml_path: Path, descriptions: Dict) -> None:
        """Inject descriptions into a single XML file"""
        try:
            # Parse XML with preserved formatting
            tree = ET.parse(xml_path)
            root = tree.getroot()
            
            modified = False
            
            # Find all ThingDef elements
            for thing_def in root.findall(".//ThingDef"):
                def_name = self.get_xml_text(thing_def.find("defName"))
                
                # Check if we have a description for this turret
                if def_name in descriptions:
                    # Update description element
                    desc_elem = thing_def.find("description")
                    new_desc = descriptions[def_name]["description"]
                    
                    if desc_elem is not None:
                        if desc_elem.text != new_desc:
                            desc_elem.text = new_desc
                            modified = True
                            print(f"    Updated: {def_name}")
                    else:
                        # Create new description element if it doesn't exist
                        desc_elem = ET.SubElement(thing_def, "description")
                        desc_elem.text = new_desc
                        # Insert after label if possible
                        label_elem = thing_def.find("label")
                        if label_elem is not None:
                            thing_def.remove(desc_elem)
                            label_index = list(thing_def).index(label_elem)
                            thing_def.insert(label_index + 1, desc_elem)
                        modified = True
                        print(f"    Added description: {def_name}")
            
            if modified:
                # Create backup
                backup_path = xml_path.with_suffix('.xml.bak')
                if not backup_path.exists():
                    import shutil
                    shutil.copy2(xml_path, backup_path)
                    print(f"    Created backup: {backup_path.name}")
                
                # Write modified XML
                tree.write(xml_path, encoding='utf-8', xml_declaration=True)
                print(f"    Saved: {xml_path.name}")
        
        except ET.ParseError as e:
            print(f"    Error processing {xml_path}: {e}")


def main():
    parser = argparse.ArgumentParser(description="Generate turret descriptions for Absolutely More Cannons")
    parser.add_argument("--generate-flavor-template", action="store_true",
                       help="Generate flavor text template file")
    parser.add_argument("--generate", action="store_true",
                       help="Generate turret descriptions JSON")
    parser.add_argument("--inject", action="store_true",
                       help="Inject descriptions from JSON into XML files")
    parser.add_argument("--test-mode", action="store_true",
                       help="Only process test turrets (120mmTAK120, 88mmFlak41)")
    parser.add_argument("--flavor-file", type=str, default="turret_flavor_text.txt",
                       help="Path to flavor text file")
    parser.add_argument("--output", type=str, default="turret_descriptions.json",
                       help="Output JSON file")
    
    args = parser.parse_args()
    
    # Determine mod path (current directory)
    mod_path = Path.cwd()
    
    generator = TurretDescriptionGenerator(mod_path, test_mode=args.test_mode)
    
    if args.generate_flavor_template:
        output_file = mod_path / args.flavor_file
        generator.generate_flavor_template(output_file)
    
    elif args.generate:
        # Load flavor texts
        flavor_file = mod_path / args.flavor_file
        generator.load_flavor_texts(flavor_file)
        
        # Scan and parse
        generator.scan_turrets()
        generator.parse_ammo_sets()
        generator.parse_ammo_stats()
        
        # Generate descriptions
        descriptions = generator.generate_descriptions()
        
        # Save to JSON
        output_file = mod_path / args.output
        generator.save_descriptions(descriptions, output_file)
    
    elif args.inject:
        descriptions_file = mod_path / args.output
        generator.inject_descriptions(descriptions_file)
    
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
