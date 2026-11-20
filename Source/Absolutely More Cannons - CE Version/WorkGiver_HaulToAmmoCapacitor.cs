using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class WorkGiver_HaulToAmmoCapacitor : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building building = t as Building;
            if (building == null || building.Faction != pawn.Faction) return false;
            if (building.IsForbidden(pawn)) return false;

            var comp = building.TryGetComp<CompAmmoCapacitor>();
            if (comp == null) return false;

            // Allow multiple pawns to haul to the same machine simultaneously
            if (!pawn.CanReach(building, PathEndMode.Touch, Danger.Deadly)) return false;

            // 1. Check if an active machine upgrade needs materials delivered
            if (comp.isUpgrading && !comp.AreUpgradeMaterialsSatisfied)
            {
                foreach (var req in comp.GetMissingUpgradeMaterials())
                {
                    Thing found = FindClosestMaterial(pawn, req.thingDef);
                    if (found != null) return true;
                }
            }

            // 2. Check if a specialization module is requested
            if (comp.targetModuleDef != null && (!comp.HasModule || comp.InstalledModule.def != comp.targetModuleDef))
            {
                Thing modItem = FindClosestMaterial(pawn, comp.targetModuleDef);
                if (modItem != null) return true;
            }

            // 3. Check if any capacitor materials need hauling
            foreach (ThingDef matDef in comp.GetAllNeededHaulMaterials())
            {
                int needed = comp.GetTarget(matDef) - comp.GetStored(matDef);
                if (needed <= 0) continue;

                Thing found = FindClosestMaterial(pawn, matDef);
                if (found != null) return true;
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building building = t as Building;
            if (building == null) return null;

            var comp = building.TryGetComp<CompAmmoCapacitor>();
            if (comp == null) return null;

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AMC_Job_HaulToAmmoCapacitor");
            if (jobDef == null) return null;

            // 1. Highest Priority: Deliver required materials for an active machine upgrade
            if (comp.isUpgrading && !comp.AreUpgradeMaterialsSatisfied)
            {
                foreach (var req in comp.GetMissingUpgradeMaterials())
                {
                    Thing found = FindClosestMaterial(pawn, req.thingDef);
                    if (found != null)
                    {
                        int maxCarry = pawn.carryTracker?.MaxStackSpaceEver(req.thingDef) ?? req.thingDef.stackLimit;
                        int haulCount = Mathf.Min(req.count, found.stackCount, Mathf.Max(1, maxCarry));
                        Job job = JobMaker.MakeJob(jobDef, building, found);
                        job.count = haulCount;
                        return job;
                    }
                }
            }

            // 2. Priority: Haul assigned specialization module if requested
            if (comp.targetModuleDef != null && (!comp.HasModule || comp.InstalledModule.def != comp.targetModuleDef))
            {
                Thing modItem = FindClosestMaterial(pawn, comp.targetModuleDef);
                if (modItem != null)
                {
                    Job job = JobMaker.MakeJob(jobDef, building, modItem);
                    job.count = 1;
                    return job;
                }
            }

            // 3. Haul capacitor raw materials
            foreach (ThingDef matDef in comp.GetAllNeededHaulMaterials())
            {
                int needed = comp.GetTarget(matDef) - comp.GetStored(matDef);
                if (needed <= 0) continue;

                Thing found = FindClosestMaterial(pawn, matDef);
                if (found != null)
                {
                    int maxCarry = pawn.carryTracker?.MaxStackSpaceEver(matDef) ?? matDef.stackLimit;
                    int haulCount = Mathf.Min(needed, found.stackCount, Mathf.Max(1, maxCarry));

                    Job job = JobMaker.MakeJob(jobDef, building, found);
                    job.count = haulCount;
                    return job;
                }
            }

            return null;
        }

        private Thing FindClosestMaterial(Pawn pawn, ThingDef matDef)
        {
            if (pawn?.Map == null || matDef == null) return null;

            List<Thing> items = pawn.Map.listerThings.ThingsOfDef(matDef);
            if (items == null || items.Count == 0) return null;

            Thing closestItem = null;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < items.Count; i++)
            {
                Thing item = items[i];
                if (item != null && item.Spawned && !item.IsForbidden(pawn) &&
                    pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Deadly))
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
