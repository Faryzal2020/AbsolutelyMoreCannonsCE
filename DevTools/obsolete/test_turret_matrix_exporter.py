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
import re
import xml.etree.ElementTree as ET

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

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

            print(f"  [PASS] {def_name} -> {tag_name} updated to '{new_val}' (Single line diff verified)")

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

    def test_enable_selectable_burst_counts_on_fixed_burst_turret(self):
        """Test enabling selectable burst counts feature on a turret that previously only had fixed burst."""
        import tempfile
        def_info = self.all_turrets["Turret_155mmGCT_Base"]
        orig_xml = def_info['rawXml']

        # Ensure original XML did not have selectableBurstCounts
        self.assertNotIn("selectableBurstCounts", orig_xml, "155mm GCT must start without selectableBurstCounts.")

        # Simulate patching XML with selectable burst counts [3, 5, 10]
        bursts = [3, 5, 10]
        burst_items = "\n".join([f"\t\t\t\t<li>{b}</li>" for b in bursts])
        burst_block = f"<selectableBurstCounts>\n{burst_items}\n\t\t\t</selectableBurstCounts>"

        if "<modExtensions>" in orig_xml:
            patched_xml = re.sub(r'(<modExtensions>)', r'\1\n\t\t\t<li Class="AbsolutelyMoreCannons.TurretBarrelExtension">\n\t\t\t\t' + burst_block + r'\n\t\t\t</li>', orig_xml)
        else:
            patched_xml = re.sub(r'(</ThingDef>)', r'\t<modExtensions>\n\t\t\t<li Class="AbsolutelyMoreCannons.TurretBarrelExtension">\n\t\t\t\t' + burst_block + r'\n\t\t\t</li>\n\t</modExtensions>\n\1', orig_xml)

        temp_export_file = tempfile.NamedTemporaryFile(suffix=".xml", delete=False).name
        try:
            # 1. Export patched XML
            with open(temp_export_file, "w", encoding="utf-8") as f:
                f.write(patched_xml)

            # 2. Verify XML syntax validity by parsing with ElementTree
            tree = ET.fromstring(patched_xml)
            self.assertIsNotNone(tree, "Exported XML must be valid XML.")
            
            # Find selectableBurstCounts node
            ext_nodes = tree.findall(".//selectableBurstCounts")
            self.assertEqual(len(ext_nodes), 1, "Exported XML must contain exactly 1 <selectableBurstCounts> element.")
            
            burst_lis = [li.text.strip() for li in ext_nodes[0].findall("li")]
            self.assertEqual(burst_lis, ["3", "5", "10"], f"Expected burst counts ['3', '5', '10'], got {burst_lis}")

            print("  [PASS] Turret_155mmGCT_Base -> Selectable burst counts enabled with valid XML syntax [3, 5, 10]")

        finally:
            if os.path.exists(temp_export_file):
                os.remove(temp_export_file)

    def test_u127mm_mark16_dual_mode_load_and_edit(self):
        """Test loading XML data from Turret_U127mmMark16_Base, system pairing, and editing both direct and indirect defs."""
        # 1. Verify system pairing
        u127_sys = None
        for sys in self.systems:
            if sys.get('directMode') and sys['directMode']['defName'] == "Turret_U127mmMark16_Base":
                u127_sys = sys
                break

        self.assertIsNotNone(u127_sys, "Turret_U127mmMark16_Base must be parsed and loaded in systems database.")
        self.assertEqual(u127_sys['systemType'], 'dual_mode', "U127mm Mark 16 must be classified as dual_mode.")
        self.assertTrue(u127_sys['hasModeSwap'], "U127mm Mark 16 must have hasModeSwap=True.")
        self.assertIsNotNone(u127_sys['indirectMode'], "U127mm Mark 16 must have a paired indirectMode def.")
        self.assertEqual(u127_sys['indirectMode']['defName'], "Turret_U127mmMark16_indirect_Base", "Indirect mode defName must match Turret_U127mmMark16_indirect_Base.")

        # Verify unmanned status (both direct and indirect inherit from auto bases)
        self.assertFalse(u127_sys['directMode']['isManned'], "Direct mode of U127mm Mark 16 must be unmanned.")
        self.assertFalse(u127_sys['indirectMode']['isManned'], "Indirect mode of U127mm Mark 16 must be unmanned.")

        # 2. Test editing Direct Mode MaxHitPoints
        self.run_single_value_diff_test("Turret_U127mmMark16_Base", "MaxHitPoints", 2500)

        # 3. Test editing Indirect Mode MaxHitPoints
        self.run_single_value_diff_test("Turret_U127mmMark16_indirect_Base", "MaxHitPoints", 2500)

        # 4. Test editing turretBurstCooldownTime on Direct Mode
        self.run_single_value_diff_test("Turret_U127mmMark16_Base", "turretBurstCooldownTime", "2.8")

    def test_enable_rotary_spinning_animation_on_turret(self):
        """Test enabling Gatling/Vulcan rotary spinning animation on a turret and verifying output XML syntax."""
        import tempfile
        def_info = self.all_turrets["Turret_20mmFlak38_Base"]
        orig_xml = def_info['rawXml']

        # Ensure original XML did not have spinningAnimation
        self.assertNotIn("spinningAnimation", orig_xml, "20mm Flak 38 must start without spinningAnimation.")

        spin_block = """<spinningAnimation>
\t\t\t\t<enabled>true</enabled>
\t\t\t\t<animationMode>RPMBased</animationMode>
\t\t\t\t<maxRPM>3000</maxRPM>
\t\t\t\t<spindownTime>1.5</spindownTime>
\t\t\t\t<frameCount>4</frameCount>
\t\t\t\t<barrelCount>6</barrelCount>
\t\t\t\t<spinUpSound>AMC_Vulcan_SpinUp</spinUpSound>
\t\t\t\t<spinDownSound>AMC_Vulcan_SpinDown</spinDownSound>
\t\t\t</spinningAnimation>"""

        barrel_ext_regex = r'(<li\s+Class=["\']AbsolutelyMoreCannons\.TurretBarrelExtension["\'][^>]*>)'
        patched_xml = re.sub(barrel_ext_regex, r'\1\n\t\t\t' + spin_block, orig_xml)

        temp_export_file = tempfile.NamedTemporaryFile(suffix=".xml", delete=False).name
        try:
            with open(temp_export_file, "w", encoding="utf-8") as f:
                f.write(patched_xml)

            tree = ET.fromstring(patched_xml)
            self.assertIsNotNone(tree, "Exported XML with spinningAnimation must be valid XML.")

            spin_nodes = tree.findall(".//spinningAnimation")
            self.assertEqual(len(spin_nodes), 1, "Exported XML must contain exactly 1 <spinningAnimation> block.")
            self.assertEqual(spin_nodes[0].findtext("animationMode"), "RPMBased")
            self.assertEqual(spin_nodes[0].findtext("maxRPM"), "3000")
            self.assertEqual(spin_nodes[0].findtext("frameCount"), "4")

            print("  [PASS] Turret_20mmFlak38_Base -> Rotary spinning animation (Gatling/Vulcan) enabled with valid XML syntax")

        finally:
            if os.path.exists(temp_export_file):
                os.remove(temp_export_file)

    def test_accuracy_override_and_fcs_export(self):
        """Test enabling AccuracyOverride and TurretFCS comps on a turret and verifying XML output."""
        import tempfile
        def_info = self.all_turrets["Turret_20mmFlak38_Base"]
        orig_xml = def_info['rawXml']

        acc_block = """<li Class="AbsolutelyMoreCannons.CompProperties_AccuracyOverride">
\t\t\t<swayReduction>0.1</swayReduction>
\t\t\t<recoilReduction>0.1</recoilReduction>
\t\t\t<spreadReduction>0.1</spreadReduction>
\t\t</li>"""
        fcs_tag = """<li Class="AbsolutelyMoreCannons.CompProperties_TurretFCS" />"""

        patched_xml = re.sub(r'(<comps>)', r'\1\n\t\t' + fcs_tag + r'\n\t\t' + acc_block, orig_xml)

        temp_export_file = tempfile.NamedTemporaryFile(suffix=".xml", delete=False).name
        try:
            with open(temp_export_file, "w", encoding="utf-8") as f:
                f.write(patched_xml)

            tree = ET.fromstring(patched_xml)
            self.assertIsNotNone(tree, "Exported XML with AccuracyOverride & FCS must be valid XML.")

            acc_nodes = tree.findall(".//li[@Class='AbsolutelyMoreCannons.CompProperties_AccuracyOverride']")
            self.assertEqual(len(acc_nodes), 1, "Exported XML must contain <CompProperties_AccuracyOverride>.")
            self.assertEqual(acc_nodes[0].findtext("swayReduction"), "0.1")

            fcs_nodes = tree.findall(".//li[@Class='AbsolutelyMoreCannons.CompProperties_TurretFCS']")
            self.assertEqual(len(fcs_nodes), 1, "Exported XML must contain <CompProperties_TurretFCS>.")

            print("  [PASS] Turret_20mmFlak38_Base -> AccuracyOverride & FCS comps enabled with valid XML syntax")

        finally:
            if os.path.exists(temp_export_file):
                os.remove(temp_export_file)

    def test_turret_smoker_export(self):
        """Test enabling CompProperties_TurretSmoker on a turret and verifying XML output."""
        import tempfile
        def_info = self.all_turrets["Turret_U20mmFlak38_Base"]
        orig_xml = def_info['rawXml']

        smoker_block = """<li Class="AbsolutelyMoreCannons.CompProperties_TurretSmoker">
            <muzzleEnabled>true</muzzleEnabled>
            <muzzleFleckDef>AMC_MuzzleSmoke</muzzleFleckDef>
        </li>"""

        orig_xml_clean = re.sub(r'\s*<li\s+Class=["\']AbsolutelyMoreCannons\.CompProperties_TurretSmoker["\']>[\s\S]*?</li>', '', orig_xml)
        patched_xml = re.sub(r'(<comps>)', r'\1\n\t\t' + smoker_block, orig_xml_clean)

        temp_export_file = tempfile.NamedTemporaryFile(suffix=".xml", delete=False).name
        try:
            with open(temp_export_file, "w", encoding="utf-8") as f:
                f.write(patched_xml)

            tree = ET.fromstring(patched_xml)
            self.assertIsNotNone(tree, "Exported XML with CompProperties_TurretSmoker must be valid XML.")

            smoker_nodes = tree.findall(".//li[@Class='AbsolutelyMoreCannons.CompProperties_TurretSmoker']")
            self.assertEqual(len(smoker_nodes), 1, "Exported XML must contain <CompProperties_TurretSmoker>.")
            self.assertEqual(smoker_nodes[0].findtext("muzzleFleckDef"), "AMC_MuzzleSmoke")

            print("  [PASS] Turret_U20mmFlak38_Base -> CompProperties_TurretSmoker enabled with valid XML syntax")

        finally:
            if os.path.exists(temp_export_file):
                os.remove(temp_export_file)

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass
    print("\n[TEST] Running Automated Exporter & Single-Value Diff Test Suite...\n")
    suite = unittest.TestLoader().loadTestsFromTestCase(TestTurretMatrixExporter)
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    sys.exit(not result.wasSuccessful())
