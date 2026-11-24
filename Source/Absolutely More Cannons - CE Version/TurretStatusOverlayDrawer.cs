using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Draws blinking status icons (NO_FCS, NO_AMMO) on turrets when applicable.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TurretStatusOverlayDrawer
    {
        private static readonly Material NoFcsMat = MaterialPool.MatFrom("Things/Icons/NO_FCS", ShaderDatabase.MetaOverlay);
        private static readonly Material NoAmmoMat = MaterialPool.MatFrom("Things/Icons/NO_AMMO", ShaderDatabase.MetaOverlay);

        private const float DualIconOffsetX = 0.42f;
        private const float AltitudeOffset = 0.03846154f;

        public static void DrawStatusOverlays(Building_Turret turret)
        {
            if (turret == null || !turret.Spawned)
                return;

            bool showNoFcs = ShouldShowNoFcs(turret);
            bool showNoAmmo = ShouldShowNoAmmo(turret);

            if (!showNoFcs && !showNoAmmo)
                return;

            float t = (Time.realtimeSinceStartup + 397f * (turret.thingIDNumber % 571)) * 4f;
            float pulse = (Mathf.Sin(t) + 1f) * 0.5f;
            pulse = 0.3f + pulse * 0.7f;

            Vector3 basePos = turret.TrueCenter();
            basePos.y = AltitudeLayer.MetaOverlays.AltitudeFor() + AltitudeOffset;

            if (showNoFcs && showNoAmmo)
            {
                DrawIcon(NoFcsMat, basePos + new Vector3(-DualIconOffsetX, 0f, 0f), pulse);
                DrawIcon(NoAmmoMat, basePos + new Vector3(DualIconOffsetX, 0f, 0f), pulse);
            }
            else if (showNoFcs)
            {
                DrawIcon(NoFcsMat, basePos, pulse);
            }
            else
            {
                DrawIcon(NoAmmoMat, basePos, pulse);
            }
        }

        private static bool ShouldShowNoFcs(Building_Turret turret)
        {
            var fcsComp = turret.TryGetComp<CompTurretFCS>();
            if (fcsComp == null || fcsComp.HasFCS)
                return false;

            var powerComp = turret.TryGetComp<CompPowerTrader>();
            return powerComp != null && powerComp.PowerOn;
        }

        private static bool ShouldShowNoAmmo(Building_Turret turret)
        {
            if (turret.def?.modContentPack != TurretBarrelAnimationMod.ModContent)
                return false;

            Thing gunThing = (turret as Building_TurretGunCE)?.Gun ?? (turret as Building_TurretGun)?.gun ?? turret;
            var ammoComp = (gunThing as ThingWithComps)?.TryGetComp<CompAmmoUser>();
            if (ammoComp == null)
                return false;

            return ammoComp.UseAmmo && ammoComp.MagSize > 0 && ammoComp.CurMagCount <= 0;
        }

        private static void DrawIcon(Material baseMat, Vector3 pos, float alpha)
        {
            Material fadedMat = FadedMaterialPool.FadedVersionOf(baseMat, alpha);
            Graphics.DrawMesh(MeshPool.plane08, pos, Quaternion.identity, fadedMat, 0);
        }
    }
}
