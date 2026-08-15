using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using CombatExtended;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patch for CombatExtended.CompCharges.GetChargeBracket.
    /// Intercepts charge calculation for indirect fire weapons and applies a configurable charge boost
    /// (+1 charge index by default, up to max charge) for turrets with TurretChargeBoostExtension.
    /// This forces high-velocity steep plunging arcs (~73°) instead of low-velocity shallow arcs (~50°),
    /// preventing early airburst detonations and target deviation.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_ChargeBoost
    {
        static HarmonyPatches_ChargeBoost()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.ChargeBoost");
            TryPatchGetChargeBracket(harmony);
        }

        private static void TryPatchGetChargeBracket(Harmony harmony)
        {
            try
            {
                var compType = AccessTools.TypeByName("CombatExtended.CompCharges");
                if (compType == null)
                {
                    Log.Warning("[AMC] Could not find CombatExtended.CompCharges for charge boost patching");
                    return;
                }

                var method = AccessTools.Method(compType, "GetChargeBracket", new Type[] { typeof(float), typeof(float), typeof(float), typeof(Vector2).MakeByRefType() });
                if (method != null)
                {
                    harmony.Patch(
                        original: method,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_ChargeBoost), nameof(Postfix_GetChargeBracket))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched CombatExtended.CompCharges.GetChargeBracket for Indirect Fire Charge Boost.");
                    }
                }
                else
                {
                    Log.Warning("[AMC] Could not find CompCharges.GetChargeBracket method to patch");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching CompCharges.GetChargeBracket: {ex}");
            }
        }

        public static void Postfix_GetChargeBracket(CompCharges __instance, float range, float shotHeight, float gravityPerWidth, ref Vector2 bracket, ref bool __result)
        {
            try
            {
                if (!__result || __instance == null || __instance.Props == null || __instance.Props.chargeSpeeds == null) return;

                var speeds = __instance.Props.chargeSpeeds;
                if (speeds.Count <= 1) return;

                int offset = GetChargeOffset(__instance.parent);
                if (offset <= 0) return;

                float currentSpeed = bracket.x;
                int currentIndex = speeds.IndexOf(Mathf.RoundToInt(currentSpeed));
                if (currentIndex < 0) return;

                int boostedIndex = Mathf.Min(currentIndex + offset, speeds.Count - 1);
                if (boostedIndex != currentIndex)
                {
                    float boostedSpeed = speeds[boostedIndex];
                    float maxRangeAngle = Mathf.Deg2Rad * 45f;
                    float boostedMaxRange = CE_Utility.MaxProjectileRange(shotHeight, boostedSpeed, maxRangeAngle, gravityPerWidth);
                    bracket = new Vector2(boostedSpeed, boostedMaxRange);

                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        AMCLogger.LogFCS($"[AMC CHARGE BOOST] Weapon '{__instance.parent?.LabelCap}' boosted charge index {currentIndex} ({currentSpeed:F1} m/s) -> {boostedIndex} ({boostedSpeed:F1} m/s) for range {range:F1} tiles");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_GetChargeBracket: {ex}");
            }
        }

        private static int GetChargeOffset(Thing weapon)
        {
            if (weapon == null) return 0;

            // 1. Weapon's own DefModExtension
            var ext = weapon.def?.GetModExtension<TurretChargeBoostExtension>();
            if (ext != null && ext.enabled) return ext.chargeOffset;

            // 2. Parent turret holder's DefModExtension or CompProperties
            if (weapon.ParentHolder is Building_Turret turret)
            {
                var turretExt = turret.def?.GetModExtension<TurretChargeBoostExtension>();
                if (turretExt != null && turretExt.enabled) return turretExt.chargeOffset;

                var barrelComp = turret.TryGetComp<CompTurretBarrel>();
                if (barrelComp?.Props != null && barrelComp.Props.chargeBoostOffset > 0)
                {
                    return barrelComp.Props.chargeBoostOffset;
                }
            }

            // 3. Weapon is turret building directly
            if (weapon is Building_Turret bTurret)
            {
                var barrelComp = bTurret.TryGetComp<CompTurretBarrel>();
                if (barrelComp?.Props != null && barrelComp.Props.chargeBoostOffset > 0)
                {
                    return barrelComp.Props.chargeBoostOffset;
                }
            }

            return 0;
        }
    }
}
