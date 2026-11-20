using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class WorkGiver_UpgradeAmmoCapacitor : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building building = t as Building;
            if (building == null || building.Faction != pawn.Faction) return false;
            if (building.IsForbidden(pawn)) return false;

            var comp = building.TryGetComp<CompAmmoCapacitor>();
            if (comp == null || !comp.isUpgrading || !comp.AreUpgradeMaterialsSatisfied) return false;

            int minSkill = comp.UpgradeMinCraftingSkill;
            int pawnSkill = pawn.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
            if (pawnSkill < minSkill)
            {
                JobFailReason.Is($"Requires Crafting skill {minSkill} (Pawn has {pawnSkill})");
                return false;
            }

            if (!pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.Deadly, 1, -1, null, forced))
                return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building building = t as Building;
            if (building == null) return null;

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AMC_Job_UpgradeAmmoCapacitor");
            if (jobDef == null) return null;

            return JobMaker.MakeJob(jobDef, building);
        }
    }
}
