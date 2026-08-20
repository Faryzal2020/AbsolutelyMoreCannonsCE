#!/usr/bin/env python3
"""
apply_matrix_edits.py - Apply exported Turret Matrix JSON changes back to original RimWorld Def XML files.
Includes automatic .bak file backups and full rollback capabilities.

Usage:
  python DevTools/apply_matrix_edits.py matrix_export.json
  python DevTools/apply_matrix_edits.py --dry-run matrix_export.json
  python DevTools/apply_matrix_edits.py --rollback
"""

import os
import sys
import json
import shutil
import argparse
import xml.etree.ElementTree as ET

MOD_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DEFS_DIR = os.path.join(MOD_ROOT, "Common", "Defs")

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

def create_backup(file_path):
    """Creates a .bak backup of file_path if it doesn't already exist."""
    bak_path = file_path + ".bak"
    if not os.path.exists(bak_path):
        shutil.copy2(file_path, bak_path)
        print(f"  [BACKUP CREATED] -> {os.path.relpath(bak_path, MOD_ROOT)}")

def rollback_all():
    """Restores all .xml files from their .xml.bak backup files."""
    restored_count = 0
    for root, _, files in os.walk(DEFS_DIR):
        for f in files:
            if f.endswith(".xml.bak"):
                bak_full = os.path.join(root, f)
                xml_full = bak_full[:-4] # strip .bak
                shutil.copy2(bak_full, xml_full)
                os.remove(bak_full)
                restored_count += 1
                print(f"  [RESTORED] {os.path.relpath(xml_full, MOD_ROOT)}")
    if restored_count > 0:
        print(f"\n[OK] Rollback complete: Restored {restored_count} XML file(s).")
    else:
        print("\n[INFO] No backup files (.xml.bak) found to restore.")

def find_def_block(content, def_name):
    """Finds exact (start, end) character range of the <ThingDef>...</ThingDef> block containing <defName>def_name</defName>."""
    import re
    def_name_pattern = re.compile(rf'<defName>\s*{re.escape(def_name)}\s*</defName>')
    def_match = def_name_pattern.search(content)
    if not def_match:
        return None

    def_start = def_match.start()
    def_end = def_match.end()

    start_pos = content.rfind('<ThingDef', 0, def_start)
    if start_pos == -1:
        return None

    if content.rfind('</ThingDef>', start_pos, def_start) != -1:
        return None

    end_tag_pos = content.find('</ThingDef>', def_end)
    if end_tag_pos == -1:
        return None

    end_pos = end_tag_pos + len('</ThingDef>')
    return (start_pos, end_pos)

def apply_edits(json_file, dry_run=False):
    if not os.path.exists(json_file):
        print(f"Error: Export file not found: {json_file}")
        sys.exit(1)

    with open(json_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    turret_edits = data.get("modifiedDefs", {})
    if not turret_edits:
        print("No modified defs found in JSON export file.")
        return

    print(f"Processing edits for {len(turret_edits)} def(s)...")
    if dry_run:
        print(" [DRY-RUN MODE] No files will be modified on disk.")

    files_to_edits = {}
    for def_name, def_payload in turret_edits.items():
        # Main building Def
        rel_path = def_payload.get("filePath")
        if rel_path:
            files_to_edits.setdefault(rel_path, []).append((def_name, def_payload))

        # Nested weapon Def if present
        weapon_payload = def_payload.get("weapon")
        if isinstance(weapon_payload, dict):
            w_def_name = weapon_payload.get("weaponDefName")
            w_rel_path = weapon_payload.get("filePath") or rel_path
            if w_def_name and w_rel_path:
                files_to_edits.setdefault(w_rel_path, []).append((w_def_name, weapon_payload))

    updated_files = set()

    for rel_path, edits in files_to_edits.items():
        abs_path = os.path.join(MOD_ROOT, rel_path)
        if not os.path.exists(abs_path):
            print(f"  [WARN] Target XML file does not exist: {rel_path}")
            continue

        with open(abs_path, "r", encoding="utf-8") as f:
            content = f.read()

        file_modified = False
        for def_name, def_payload in edits:
            raw_xml = (def_payload.get("rawXml") or def_payload.get("defXml") or "").strip()
            if not raw_xml:
                print(f"  [WARN] No raw XML content found for def {def_name}")
                continue

            block_range = find_def_block(content, def_name)
            if block_range:
                start_pos, end_pos = block_range
                content = content[:start_pos] + raw_xml + content[end_pos:]
                file_modified = True
                print(f"  [OK] Replaced def {def_name} in {rel_path}")
            else:
                print(f"  [WARN] Could not find exact <ThingDef> block for {def_name} in {rel_path}")

        if file_modified:
            if not dry_run:
                create_backup(abs_path)
                with open(abs_path, "w", encoding="utf-8") as f:
                    f.write(content)
            updated_files.add(rel_path)

    print(f"\nDone. Processed {len(updated_files)} file(s).")
    if updated_files and not dry_run:
        print("[INFO] Backup (.bak) files were created before editing. Run with --rollback to undo.")

def main():
    parser = argparse.ArgumentParser(description="AMC Turret Matrix XML Exporter & Sync Engine")
    parser.add_argument("json_file", nargs="?", help="Path to exported matrix JSON file")
    parser.add_argument("--dry-run", action="store_true", help="Simulate changes without writing to disk")
    parser.add_argument("--rollback", action="store_true", help="Rollback all .xml files from .xml.bak backups")

    args = parser.parse_args()

    if args.rollback:
        rollback_all()
        return

    if not args.json_file:
        parser.print_help()
        sys.exit(1)

    apply_edits(args.json_file, dry_run=args.dry_run)

if __name__ == "__main__":
    main()
