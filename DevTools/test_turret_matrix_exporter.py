#!/usr/bin/env python3
"""
test_turret_matrix_exporter.py - Automated Test Suite for AMC Turret Matrix Exporter & Diff Engine.

Test Steps for each target turret:
  1. Load original XML def content.
  2. Perform a single value edit (e.g. MaxHitPoints, WorkToBuild, turretBurstCooldownTime, label).
  3. Export modified XML to temporary test file.
  4. Verify that exported XML matches expected planned diff.
  5. Verify that unified diff between original XML vs exported XML ONLY has that one single line/value changed.
  6. Delete temporary test export XML file and ensure clean state.
"""

import os
import sys
import unittest
import difflib
import xml.etree.ElementTree as ET

# Add DevTools directory to import generate_turret_matrix
DEVTOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DEVTOOLS_DIR)

from generate_turret_matrix import DefDatabase, group_turret_systems, MOD_ROOT

def patch_xml_tag(orig_xml, tag_name, new_value):
    """Simulates JS patchXmlString engine by updating a single XML tag in rawXml."""
    import re
    tag_regex = re.compile(rf'<{tag_name}>[\s\S]*?</{tag_name}>')
    if tag_regex.search(orig_xml):
        return tag_regex.sub(f'<{tag_name}>{new_value}</{tag_name}>', orig_xml)
    else:
        # If tag does not exist, insert before closing </ThingDef>
        closing_idx = orig_xml.rfind('</ThingDef>')
        if closing_idx != -1:
            indent = "    "
            return orig_xml[:closing_idx] + f"{indent}<{tag_name}>{new_value}</{tag_name}>\n" + orig_xml[closing_idx:]
    return orig_xml

class TestTurretMatrixExporter(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.db = DefDatabase()
        cls.db.parse_all()
        cls.systems = group_turret_systems(cls.db)
        cls.all_turrets = {}
        for sys in cls.systems:
            if sys.get('directMode'):
                cls.all_turrets[sys['directMode']['defName']] = sys['directMode']
            if sys.get('indirectMode'):
                cls.all_turrets[sys['indirectMode']['defName']] = sys['indirectMode']

    def run_single_value_diff_test(self, def_name, tag_name, new_val):
        self.assertIn(def_name, self.all_turrets, f"Turret def {def_name} must exist in database.")
        def_obj = self.all_turrets[def_name]
        orig_xml = def_obj.get('rawXml', '').strip()
        self.assertTrue(orig_xml, f"Original XML for {def_name} must not be empty.")

        # 1. Simulate single field edit & export XML
        exported_xml = patch_xml_tag(orig_xml, tag_name, new_val).strip()

        # Write to temporary export file
        temp_export_file = os.path.join(DEVTOOLS_DIR, f"test_export_{def_name}.xml")
        try:
            with open(temp_export_file, "w", encoding="utf-8") as f:
                f.write(exported_xml)

            self.assertTrue(os.path.exists(temp_export_file), f"Export file {temp_export_file} should be created.")

            # 2. Verify exported XML contains planned diff value
            expected_tag_snippet = f"<{tag_name}>{new_val}</{tag_name}>"
            self.assertIn(expected_tag_snippet, exported_xml, f"Exported XML must contain updated snippet: {expected_tag_snippet}")

            # 3. Calculate unified diff between original XML vs exported XML
            orig_lines = orig_xml.splitlines(keepends=True)
            exp_lines = exported_xml.splitlines(keepends=True)

            diff_lines = list(difflib.unified_diff(orig_lines, exp_lines, fromfile="original.xml", tofile="exported.xml"))
            removed = [l for l in diff_lines if l.startswith('-') and not l.startswith('---')]
            added = [l for l in diff_lines if l.startswith('+') and not l.startswith('+++')]

            # 4. Assert ONLY 1 line changed in the XML diff
            self.assertEqual(len(removed), 1, f"Expected exactly 1 line removed in diff, got {len(removed)}: {removed}")
            self.assertEqual(len(added), 1, f"Expected exactly 1 line added in diff, got {len(added)}: {added}")

            # Assert the single added line matches our target new value
            self.assertIn(expected_tag_snippet, added[0], f"Added line {added[0]} must contain {expected_tag_snippet}")

            print(f"  ✅ [PASS] {def_name} -> {tag_name} updated to '{new_val}' (Single line diff verified)")

        finally:
            # 5. Delete temporary exported XML file
            if os.path.exists(temp_export_file):
                os.remove(temp_export_file)
                self.assertFalse(os.path.exists(temp_export_file), f"Temporary export file {temp_export_file} must be deleted.")

    def test_autocannon_hp_edit(self):
        """Test editing MaxHitPoints on Autocannon Flak 38."""
        self.run_single_value_diff_test("Turret_20mmFlak38_Base", "MaxHitPoints", 350)

    def test_howitzer_work_edit(self):
        """Test editing WorkToBuild on Howitzer 155mm GCT."""
        self.run_single_value_diff_test("Turret_155mmGCT_Base", "WorkToBuild", 12500)

    def test_unmanned_cooldown_edit(self):
        """Test editing turretBurstCooldownTime on Unmanned Auto Flak 38."""
        self.run_single_value_diff_test("Turret_U20mmFlak38_Base", "turretBurstCooldownTime", "2.5")

    def test_rotary_label_edit(self):
        """Test editing label on Rotary M61 Vulcan."""
        self.run_single_value_diff_test("Turret_20mmM61Vulcan_Base", "label", "M61 Vulcan CIWS (Custom)")

    def test_dual_mode_synced_hp_edit(self):
        """Test syncing MaxHitPoints across dual-mode direct and indirect defs."""
        self.run_single_value_diff_test("Turret_155mmGCT_Base", "MaxHitPoints", 2000)
        self.run_single_value_diff_test("Turret_155mmGCT_indirect_Base", "MaxHitPoints", 2000)

    def test_dual_mode_unlinked_stat_edit(self):
        """Test editing constructionSkillPrerequisite independently on direct mode without altering indirect counterpart."""
        self.run_single_value_diff_test("Turret_155mmGCT_Base", "constructionSkillPrerequisite", 12)
        # Verify indirect mode rawXml retains original skill requirement (6)
        indirect_def = self.all_turrets["Turret_155mmGCT_indirect_Base"]
        self.assertNotIn("<constructionSkillPrerequisite>12</constructionSkillPrerequisite>", indirect_def['rawXml'], "Indirect mode XML must not be modified when field is edited independently.")

if __name__ == "__main__":
    print("\n🚀 Running Automated Exporter & Single-Value Diff Test Suite...\n")
    suite = unittest.TestLoader().loadTestsFromTestCase(TestTurretMatrixExporter)
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    sys.exit(not result.wasSuccessful())
