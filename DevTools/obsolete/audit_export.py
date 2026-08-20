import os
import sys
import json
import xml.etree.ElementTree as ET

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

def audit_json_export(export_path):
    print(f"=== COMPREHENSIVE MATRIX EXPORT AUDIT: {export_path} ===\n")
    if not os.path.exists(export_path):
        print(f"ERROR: Export file {export_path} does not exist.")
        return

    with open(export_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    modified_defs = data.get("modifiedDefs", {})
    print(f"Found {len(modified_defs)} modified def(s) in export.\n")

    for def_name, def_data in modified_defs.items():
        print(f"==================================================")
        print(f"  TARGET DEF: {def_name}")
        print(f"==================================================")
        rel_path = def_data.get("filePath")
        print(f"Target File: {rel_path}")
        print(f"File Exists: {os.path.exists(rel_path)}\n")

        raw_xml = def_data.get("rawXml", "")
        weapon_data = def_data.get("weapon", {})
        w_raw_xml = weapon_data.get("rawXml", "")

        # Parse JSON rawXml payloads
        try:
            b_root = ET.fromstring(raw_xml)
            print(f"[OK] JSON rawXml (Building): Valid XML (<{b_root.tag}> defName='{b_root.findtext('defName')}')")
        except Exception as e:
            print(f"[FAIL] JSON rawXml (Building): Invalid XML - {e}")
            b_root = None

        try:
            w_root = ET.fromstring(w_raw_xml)
            print(f"[OK] JSON rawXml (Weapon): Valid XML (<{w_root.tag}> defName='{w_root.findtext('defName')}')\n")
        except Exception as e:
            print(f"[FAIL] JSON rawXml (Weapon): Invalid XML - {e}")
            w_root = None

        # Parse Disk File XML
        disk_b_root = None
        disk_w_root = None
        if os.path.exists(rel_path):
            try:
                tree = ET.parse(rel_path)
                defs_node = tree.getroot()
                for thing_def in defs_node.findall("ThingDef"):
                    name = thing_def.findtext("defName")
                    if name == def_name:
                        disk_b_root = thing_def
                    elif name == weapon_data.get("weaponDefName"):
                        disk_w_root = thing_def
            except Exception as e:
                print(f"[WARN] Error reading disk file {rel_path}: {e}")

        # ---------------------------------------------------------
        # 1. BUILDING DEF AUDIT
        # ---------------------------------------------------------
        print("[BUILDING DEF PARAMETER AUDIT]")
        audits = []

        if b_root is not None:
            # Core Stats
            audits.append(("label", str(def_data.get("label")), str(b_root.findtext("label"))))
            audits.append(("MaxHitPoints", str(def_data.get("hp")), str(b_root.findtext("statBases/MaxHitPoints"))))
            audits.append(("WorkToBuild", str(def_data.get("work")), str(b_root.findtext("statBases/WorkToBuild"))))
            audits.append(("Mass", str(def_data.get("mass")), str(b_root.findtext("statBases/Mass"))))
            audits.append(("Bulk", str(def_data.get("bulk")), str(b_root.findtext("statBases/Bulk"))))
            audits.append(("SkillReq", str(def_data.get("skillReq")), str(b_root.findtext("constructionSkillPrerequisite"))))
            audits.append(("Cooldown", str(def_data.get("cooldownTime")), str(b_root.findtext("building/turretBurstCooldownTime"))))
            audits.append(("TopDrawSize", str(def_data.get("topDrawSize")), str(b_root.findtext("building/turretTopDrawSize"))))

            # Power
            power_node = b_root.find(".//li[@Class='CompProperties_Power']/basePowerConsumption")
            xml_power = str(power_node.text) if power_node is not None else "None"
            audits.append(("basePowerConsumption", str(def_data.get("powerWatts")), xml_power))

            # Comps
            fcs_node = b_root.find(".//li[@Class='AbsolutelyMoreCannons.CompProperties_TurretFCS']")
            audits.append(("CompProperties_TurretFCS", str(def_data.get("hasFcs")), str(fcs_node is not None)))

            acc_json = def_data.get("accuracyOverride", {})
            acc_node = b_root.find(".//li[@Class='AbsolutelyMoreCannons.CompProperties_AccuracyOverride']")
            audits.append(("CompProperties_AccuracyOverride", str(acc_json.get("hasAccuracyOverride")), str(acc_node is not None)))

            smk_json = def_data.get("smokerData", {})
            smk_node = b_root.find(".//li[@Class='AbsolutelyMoreCannons.CompProperties_TurretSmoker']")
            audits.append(("CompProperties_TurretSmoker", str(smk_json.get("hasSmoker")), str(smk_node is not None)))
            if smk_node is not None and smk_json:
                audits.append(("smoker.muzzleEnabled", str(smk_json.get("muzzleEnabled")).lower(), str(smk_node.findtext("muzzleEnabled")).lower()))
                audits.append(("smoker.heatEnabled", str(smk_json.get("heatEnabled")).lower(), str(smk_node.findtext("heatEnabled")).lower()))
                audits.append(("smoker.shockwaveEnabled", str(smk_json.get("shockwaveEnabled")).lower(), str(smk_node.findtext("shockwaveEnabled")).lower()))

            # Barrel Extension & Animations
            bext_json = def_data.get("barrelExtension", {})
            bext_node = b_root.find(".//li[@Class='AbsolutelyMoreCannons.TurretBarrelExtension']")
            audits.append(("TurretBarrelExtension", str(bext_json.get("hasBarrelExtension")), str(bext_node is not None)))

            if bext_node is not None:
                rec_json = bext_json.get("recoilAnimation", {})
                rec_node = bext_node.find("recoilAnimation")
                if rec_node is not None:
                    audits.append(("recoilAnim.useRecoilCurve", str(rec_json.get("useRecoilCurve")), str(rec_node.findtext("useRecoilCurve"))))
                    audits.append(("recoilAnim.useReturnCurve", str(rec_json.get("useReturnCurve")), str(rec_node.findtext("useReturnCurve"))))
                    audits.append(("recoilAnim.affectsRotation", str(rec_json.get("affectsRotation")), str(rec_node.findtext("affectsRotation"))))

                fir_json = bext_json.get("firingAnimation", {})
                fir_node = bext_node.find("firingAnimation")
                if fir_node is not None:
                    audits.append(("firingAnim.enabled", str(fir_json.get("enabled")), str(fir_node.findtext("enabled"))))
                    audits.append(("firingAnim.durationTicks", str(fir_json.get("durationTicks")), str(fir_node.findtext("durationTicks"))))

                spin_json = bext_json.get("spinningAnimation", {})
                spin_node = bext_node.find("spinningAnimation")
                spin_enabled = spin_json.get("enabled") if isinstance(spin_json, dict) else False
                audits.append(("spinningAnimation", str(bool(spin_enabled)), str(spin_node is not None)))

            burst_json = def_data.get("hasSelectableBursts")
            burst_node = b_root.find(".//selectableBurstCounts")
            audits.append(("selectableBurstCounts", str(burst_json), str(burst_node is not None)))

        for param, j_val, x_val in audits:
            match_status = "MATCH" if j_val.lower() == x_val.lower() else "DISCREPANCY"
            tag = "[OK]" if match_status == "MATCH" else "[WARN]"
            print(f"  {tag} {param:<32}: JSON={j_val:<10} | rawXml={x_val:<10} -> {match_status}")

        # ---------------------------------------------------------
        # 2. WEAPON DEF AUDIT
        # ---------------------------------------------------------
        print("\n[WEAPON DEF PARAMETER AUDIT]")
        w_audits = []

        if w_root is not None:
            w_audits.append(("SightsEfficiency", str(weapon_data.get("sightsEfficiency")), str(w_root.findtext("statBases/SightsEfficiency"))))
            w_audits.append(("ShotSpread", str(weapon_data.get("shotSpread")), str(w_root.findtext("statBases/ShotSpread"))))
            w_audits.append(("SwayFactor", str(weapon_data.get("swayFactor")), str(w_root.findtext("statBases/SwayFactor"))))
            w_audits.append(("RangedWeapon_Cooldown", str(weapon_data.get("cooldown")), str(w_root.findtext("statBases/RangedWeapon_Cooldown"))))

            verb_node = w_root.find("verbs/li[@Class='CombatExtended.VerbPropertiesCE']")
            if verb_node is not None:
                w_audits.append(("minRange", str(weapon_data.get("minRange")), str(verb_node.findtext("minRange"))))
                w_audits.append(("range", str(weapon_data.get("maxRange")), str(verb_node.findtext("range"))))
                w_audits.append(("burstShotCount", str(weapon_data.get("burstShotCount")), str(verb_node.findtext("burstShotCount"))))
                w_audits.append(("ticksBetweenBurstShots", str(weapon_data.get("ticksBetweenBurstShots")), str(verb_node.findtext("ticksBetweenBurstShots"))))
                w_audits.append(("warmupTime", str(weapon_data.get("warmupTime")), str(verb_node.findtext("warmupTime"))))

            ammo_node = w_root.find(".//li[@Class='CombatExtended.CompProperties_AmmoUser']")
            if ammo_node is not None:
                w_audits.append(("magazineSize", str(weapon_data.get("magazineSize")), str(ammo_node.findtext("magazineSize"))))
                w_audits.append(("reloadTime", str(weapon_data.get("reloadTime")), str(ammo_node.findtext("reloadTime"))))
                w_audits.append(("ammoSet", str(weapon_data.get("ammoSet")), str(ammo_node.findtext("ammoSet"))))

        for param, j_val, x_val in w_audits:
            match_status = "MATCH" if j_val.lower() == x_val.lower() else "DISCREPANCY"
            tag = "[OK]" if match_status == "MATCH" else "[WARN]"
            print(f"  {tag} {param:<32}: JSON={j_val:<10} | rawXml={x_val:<10} -> {match_status}")

        # ---------------------------------------------------------
        # 3. DISK XML FILE INTEGRITY CHECK
        # ---------------------------------------------------------
        print("\n[PRODUCED XML FILE ON DISK AUDIT]")
        if disk_b_root is not None and disk_w_root is not None:
            print(f"  [OK] Disk file {rel_path} contains building def '<{disk_b_root.tag}> {def_name}'")
            print(f"  [OK] Disk file {rel_path} contains weapon def '<{disk_w_root.tag}> {weapon_data.get('weaponDefName')}'")

            # Verify Smoker in Disk XML
            disk_smk = disk_b_root.find(".//li[@Class='AbsolutelyMoreCannons.CompProperties_TurretSmoker']")
            if disk_smk is not None:
                print("  [OK] CompProperties_TurretSmoker is present in produced XML on disk!")
            else:
                print("  [WARN] CompProperties_TurretSmoker is MISSING from produced XML on disk!")
        else:
            print(f"  [FAIL] Failed to locate both defs in disk file {rel_path}")

if __name__ == "__main__":
    audit_json_export("DevTools/amc_matrix_export.json")
