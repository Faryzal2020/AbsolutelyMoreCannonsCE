using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class JobDriver_HaulToAmmoCapacitor : JobDriver
    {
        private const TargetIndex MachineInd = TargetIndex.A;
        private const TargetIndex MaterialInd = TargetIndex.B;

        private Building Machine => TargetThingA as Building;
        private Thing MaterialThing => TargetThingB;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Machine == null || MaterialThing == null) return false;

            int reserveCount = Mathf.Clamp(job.count, 1, MaterialThing.stackCount);
            return pawn.Reserve(MaterialThing, job, 1, reserveCount, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(MachineInd);
            this.FailOnDestroyedNullOrForbidden(MaterialInd);

            // 1. Go to material stack
            yield return Toils_Goto.GotoThing(MaterialInd, PathEndMode.ClosestTouch);

            // 2. Pick up required amount
            Toil pickUp = ToilMaker.MakeToil("PickUpCapacitorMaterial");
            pickUp.initAction = () =>
            {
                Pawn actor = pickUp.actor;
                Thing mat = MaterialThing;
                var comp = Machine?.TryGetComp<CompAmmoCapacitor>();
                if (mat == null || comp == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                // Check if hauling a specialization module
                if (mat.def.HasModExtension<FabricatorModuleExtension>())
                {
                    if (comp.targetModuleDef != mat.def)
                    {
                        actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                        return;
                    }
                    actor.carryTracker.TryStartCarry(mat, 1);
                    return;
                }

                // Check if hauling for an active upgrade OR internal capacitor
                int upgradeMissing = comp.GetUpgradeMissing(mat.def);
                int capMissing = Mathf.Max(0, comp.GetTarget(mat.def) - comp.GetStored(mat.def));
                int stillNeeded = upgradeMissing + capMissing;
                if (stillNeeded <= 0)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return;
                }

                int toCarry = Mathf.Min(stillNeeded, job.count, mat.stackCount);
                if (toCarry <= 0)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return;
                }

                actor.carryTracker.TryStartCarry(mat, toCarry);
            };
            pickUp.FailOnDestroyedNullOrForbidden(MaterialInd);
            yield return pickUp;

            // 3. Walk to the Ammo Fabricator
            yield return Toils_Goto.GotoThing(MachineInd, PathEndMode.Touch);

            // 4. Deposit into upgrade pool, internal capacitor, or install module
            Toil deposit = ToilMaker.MakeToil("DepositIntoAmmoCapacitor");
            deposit.defaultCompleteMode = ToilCompleteMode.Delay;
            deposit.defaultDuration = 35;
            deposit.WithProgressBarToilDelay(MachineInd);
            deposit.AddFinishAction(() =>
            {
                var comp = Machine?.TryGetComp<CompAmmoCapacitor>();
                Thing carried = pawn.carryTracker?.CarriedThing;
                if (comp != null && carried != null)
                {
                    if (carried.def.HasModExtension<FabricatorModuleExtension>())
                    {
                        if (comp.targetModuleDef == carried.def)
                        {
                            pawn.carryTracker.innerContainer.TryTransferToContainer(carried, comp.GetDirectlyHeldThings(), 1);
                            comp.targetModuleDef = null;
                            Messages.Message($"{pawn.LabelCap} installed {carried.LabelCap} into {Machine.LabelCap}.", Machine, MessageTypeDefOf.PositiveEvent);
                        }
                        if (pawn.carryTracker.CarriedThing != null)
                        {
                            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                        }
                        return;
                    }

                    // First satisfy any active upgrade requirement for this material
                    int upgradeMissing = comp.GetUpgradeMissing(carried.def);
                    if (upgradeMissing > 0)
                    {
                        int toUpgrade = Mathf.Clamp(upgradeMissing, 0, carried.stackCount);
                        comp.DepositUpgradeMaterial(carried, toUpgrade);
                    }

                    // Then deposit any remaining carried items into the internal capacitor if needed
                    carried = pawn.carryTracker?.CarriedThing;
                    if (carried != null && !carried.Destroyed)
                    {
                        int needed = comp.GetTarget(carried.def) - comp.GetStored(carried.def);
                        int toDeposit = Mathf.Clamp(needed, 0, carried.stackCount);
                        if (toDeposit > 0)
                        {
                            comp.DepositMaterial(carried, toDeposit);
                        }
                    }

                    // If target was lowered or upgrade cancelled while pawn was walking, drop any leftover carried items
                    if (pawn.carryTracker.CarriedThing != null)
                    {
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                    }
                }
            });
            yield return deposit;
        }
    }
}
