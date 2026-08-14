using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    public class CompTurretFCS : ThingComp, IThingHolder
    {
        private ThingOwner innerContainer;
        public ThingDef targetFCSDef;

        public CompProperties_TurretFCS Props => (CompProperties_TurretFCS)props;

        public CompTurretFCS()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        public Thing LoadedFCSItem => (innerContainer != null && innerContainer.Count > 0) ? innerContainer[0] : null;
        public bool HasFCS => LoadedFCSItem != null;

        /// <summary>
        /// Gets active FCS stat extension from loaded item, if present.
        /// </summary>
        public FCSPropertiesDefExtension ActiveStats
        {
            get
            {
                if (LoadedFCSItem == null) return null;
                return LoadedFCSItem.def.GetModExtension<FCSPropertiesDefExtension>();
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            if (innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this);
            }
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outHolders)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outHolders, GetDirectlyHeldThings());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Defs.Look(ref targetFCSDef, "targetFCSDef");
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap != null && innerContainer != null && innerContainer.Count > 0)
            {
                innerContainer.TryDropAll(parent.Position, previousMap, ThingPlaceMode.Near);
            }
        }

        /// <summary>
        /// Dynamic Gizmo button UI (CE Reload style).
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            // Only show gizmo for player-owned turrets
            if (parent.Faction != Faction.OfPlayer) yield break;

            Texture2D gizmoIcon;
            string label;
            string desc;

            if (LoadedFCSItem != null)
            {
                gizmoIcon = LoadedFCSItem.def.uiIcon;
                label = $"FCS: {LoadedFCSItem.def.label}";
                desc = $"Currently loaded: {LoadedFCSItem.def.label}.\nClick to select a different module or eject the current one.";
            }
            else if (targetFCSDef != null)
            {
                gizmoIcon = targetFCSDef.uiIcon;
                label = $"Set FCS: {targetFCSDef.label}";
                desc = $"Waiting for colonist to haul and load {targetFCSDef.label}.\nClick to change selection or cancel.";
            }
            else
            {
                gizmoIcon = ContentFinder<Texture2D>.Get("UI/Commands/LoadFCS", false) ?? ContentFinder<Texture2D>.Get("UI/Buttons/Reload", true);
                label = "Load FCS";
                desc = "No Fire Control System loaded! Turret is inoperable.\nClick to select an FCS module for colonists to install.";
            }

            yield return new Command_Action
            {
                defaultLabel = label,
                defaultDesc = desc,
                icon = gizmoIcon,
                action = () => OpenFCSSelectionMenu()
            };
        }

        private void OpenFCSSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Option 1: Unload / Eject / Clear Target
            if (HasFCS || targetFCSDef != null)
            {
                Texture2D ejectIcon = ContentFinder<Texture2D>.Get("UI/Commands/EjectFCS", false);
                options.Add(new FloatMenuOption(
                    label: "Unload / Clear FCS Target",
                    action: () =>
                    {
                        targetFCSDef = null;
                        if (HasFCS && parent.Map != null)
                        {
                            innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near);
                            Messages.Message($"Ejected Fire Control System from {parent.LabelCap}.", parent, MessageTypeDefOf.NeutralEvent);
                        }
                    },
                    itemIcon: ejectIcon,
                    iconColor: Color.white
                ));
            }

            // Option 2: List available FCS defs
            List<ThingDef> fcsTypes = new List<ThingDef>
            {
                DefDatabase<ThingDef>.GetNamedSilentFail("AMC_FCS_Basic"),
                DefDatabase<ThingDef>.GetNamedSilentFail("AMC_FCS_Advanced"),
                DefDatabase<ThingDef>.GetNamedSilentFail("AMC_FCS_Spacer")
            };

            Map map = parent.Map;
            foreach (var fcsDef in fcsTypes)
            {
                if (fcsDef == null) continue;

                int mapCount = (map != null) ? map.resourceCounter.GetCount(fcsDef) : 0;
                string statusText = mapCount > 0 ? $"Available on map: {mapCount}" : "None available on map";
                string optionLabel = $"{fcsDef.LabelCap} ({statusText})";

                options.Add(new FloatMenuOption(
                    label: optionLabel,
                    action: () =>
                    {
                        targetFCSDef = fcsDef;
                        // Eject current FCS if a different one is selected
                        if (HasFCS && LoadedFCSItem.def != fcsDef && map != null)
                        {
                            innerContainer.TryDropAll(parent.Position, map, ThingPlaceMode.Near);
                        }
                        Messages.Message($"Assigned {fcsDef.LabelCap} to {parent.LabelCap}. Colonists will haul and install it.", parent, MessageTypeDefOf.NeutralEvent);
                    },
                    itemIcon: fcsDef.uiIcon,
                    iconColor: Color.white
                ));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("No FCS items found in database", null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override string CompInspectStringExtra()
        {
            if (!HasFCS)
            {
                if (targetFCSDef != null)
                {
                    return $"FCS Required: {targetFCSDef.LabelCap} (Awaiting haul)\n<color=red>INOPERABLE: Missing FCS</color>";
                }
                return "<color=red><b>INOPERABLE:</b> Missing Fire Control System (FCS)</color>";
            }

            string result = $"FCS Loaded: {LoadedFCSItem.def.LabelCap}";
            var stats = ActiveStats;
            if (stats != null)
            {
                result += $"\n  Sway: {(1f - stats.swayMultiplier) * 100f:F0}% reduction";
                result += $"\n  Spread: {(1f - stats.spreadMultiplier) * 100f:F0}% reduction";
                result += $"\n  Recoil: {(1f - stats.recoilMultiplier) * 100f:F0}% reduction";
                if (stats.aimTimeMultiplier != 1.0f)
                {
                    result += $"\n  Aim Time: {(1f - stats.aimTimeMultiplier) * 100f:F0}% faster";
                }
                if (stats.rangeMultiplier != 1.0f)
                {
                    result += $"\n  Range: +{(stats.rangeMultiplier - 1f) * 100f:F0}% bonus";
                }
            }
            return result;
        }
    }
}
