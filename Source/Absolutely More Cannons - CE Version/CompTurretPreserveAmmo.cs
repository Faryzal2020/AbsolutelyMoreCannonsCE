using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    public class CompTurretPreserveAmmo : ThingComp
    {
        public bool preserveAmmo = true;
        private bool initialized = false;

        public CompProperties_TurretPreserveAmmo Props => (CompProperties_TurretPreserveAmmo)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && !initialized)
            {
                var ext = parent.def.GetModExtension<TurretPreserveAmmoExtension>();
                if (ext != null)
                {
                    preserveAmmo = ext.defaultPreserveAmmo;
                }
                else if (Props != null)
                {
                    preserveAmmo = Props.defaultPreserveAmmo;
                }
                initialized = true;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref preserveAmmo, "preserveAmmo", true);
            Scribe_Values.Look(ref initialized, "initializedPreserveAmmo", true);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            // Only show gizmo for player-owned turrets (or devmode)
            if (parent.Faction != Faction.OfPlayer && !Prefs.DevMode)
            {
                yield break;
            }

            var ext = parent.def.GetModExtension<TurretPreserveAmmoExtension>();
            if (ext != null && !ext.allowToggle)
            {
                yield break;
            }

            Texture2D icon = preserveAmmo
                ? (ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmoON", false) ?? ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmo", false))
                : (ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmoOFF", false) ?? ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmo", false));

            yield return new Command_Toggle
            {
                defaultLabel = preserveAmmo ? "Preserve Ammo: ON" : "Preserve Ammo: OFF",
                defaultDesc = "When enabled, the turret will immediately abort burst fire when its target is downed (unless another standing target is in line of fire), saving ammunition.",
                icon = icon,
                isActive = () => preserveAmmo,
                toggleAction = () =>
                {
                    preserveAmmo = !preserveAmmo;
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        AMCLogger.LogTurretTarget($"[Preserve Ammo Toggle] {parent.LabelCap} @ {parent.Position} set to: {preserveAmmo}");
                    }
                }
            };
        }
    }
}
