using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class JobDriver_LoadTurretFCS : JobDriver
    {
        private const TargetIndex TurretInd = TargetIndex.A;
        private const TargetIndex FCSItemInd = TargetIndex.B;

        private Building Turret => TargetThingA as Building;
        private Thing FCSItem => TargetThingB;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Turret == null || FCSItem == null) return false;

            return pawn.Reserve(Turret, job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(FCSItem, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TurretInd);
            this.FailOnDestroyedOrNull(FCSItemInd);

            // Fail if turret no longer needs loading or target FCS changed
            this.FailOn(() =>
            {
                var comp = Turret?.TryGetComp<CompTurretFCS>();
                return comp == null || comp.HasFCS || comp.targetFCSDef == null;
            });

            // 1. Walk to FCS item
            yield return Toils_Goto.GotoThing(FCSItemInd, PathEndMode.ClosestTouch);

            // 2. Pick up item from ground / stockpile
            Toil takeItem = ToilMaker.MakeToil("TakeFCSItem");
            takeItem.initAction = () =>
            {
                Pawn p = takeItem.actor;
                Thing thing = FCSItem;
                if (thing != null)
                {
                    p.carryTracker.TryStartCarry(thing, 1);
                }
            };
            takeItem.FailOnDestroyedNullOrForbidden(FCSItemInd);
            yield return takeItem;

            // 3. Walk to Turret
            yield return Toils_Goto.GotoThing(TurretInd, PathEndMode.Touch);

            // 4. Deposit into turret's CompTurretFCS container
            Toil depositToil = ToilMaker.MakeToil("DepositFCS");
            depositToil.defaultCompleteMode = ToilCompleteMode.Delay;
            depositToil.defaultDuration = 100;
            depositToil.WithProgressBarToilDelay(TurretInd);
            depositToil.AddFinishAction(() =>
            {
                var comp = Turret?.TryGetComp<CompTurretFCS>();
                if (comp != null && pawn.carryTracker?.CarriedThing != null)
                {
                    Thing carried = pawn.carryTracker.CarriedThing;
                    pawn.carryTracker.innerContainer.TryTransferToContainer(carried, comp.GetDirectlyHeldThings(), 1);
                    comp.targetFCSDef = null;
                    Messages.Message($"{pawn.LabelCap} successfully loaded {carried.LabelCap} into {Turret.LabelCap}.", Turret, MessageTypeDefOf.PositiveEvent);
                }
            });
            yield return depositToil;
        }
    }
}
