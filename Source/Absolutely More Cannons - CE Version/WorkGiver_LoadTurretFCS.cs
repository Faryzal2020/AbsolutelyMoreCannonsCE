using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class WorkGiver_LoadTurretFCS : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building turret = t as Building;
            if (turret == null || turret.Faction != pawn.Faction) return false;

            var comp = turret.TryGetComp<CompTurretFCS>();
            if (comp == null || comp.HasFCS || comp.targetFCSDef == null) return false;

            if (pawn.CanReserveAndReach(turret, PathEndMode.Touch, Danger.Deadly) == false) return false;

            // Find matching FCS item on the map
            Thing fcsItem = FindFCSItem(pawn, comp.targetFCSDef);
            return fcsItem != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building turret = t as Building;
            if (turret == null) return null;

            var comp = turret.TryGetComp<CompTurretFCS>();
            if (comp == null || comp.targetFCSDef == null) return null;

            Thing fcsItem = FindFCSItem(pawn, comp.targetFCSDef);
            if (fcsItem == null) return null;

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AMC_Job_LoadTurretFCS");
            if (jobDef == null) return null;

            return JobMaker.MakeJob(jobDef, turret, fcsItem);
        }

        private Thing FindFCSItem(Pawn pawn, ThingDef fcsDef)
        {
            if (pawn == null || pawn.Map == null || fcsDef == null) return null;

            List<Thing> items = pawn.Map.listerThings.ThingsOfDef(fcsDef);
            if (items == null || items.Count == 0) return null;

            Thing closestItem = null;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < items.Count; i++)
            {
                Thing item = items[i];
                if (item != null && item.Spawned && !item.IsForbidden(pawn) && pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    float distSq = (item.Position - pawn.Position).LengthHorizontalSquared;
                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closestItem = item;
                    }
                }
            }

            return closestItem;
        }
    }
}
