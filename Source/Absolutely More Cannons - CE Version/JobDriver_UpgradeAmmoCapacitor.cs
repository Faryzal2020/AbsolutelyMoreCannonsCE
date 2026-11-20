using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class JobDriver_UpgradeAmmoCapacitor : JobDriver
    {
        private const TargetIndex MachineInd = TargetIndex.A;
        private Building Machine => TargetThingA as Building;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Machine == null) return false;
            return pawn.Reserve(Machine, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(MachineInd);
            this.FailOn(() =>
            {
                var comp = Machine?.TryGetComp<CompAmmoCapacitor>();
                return comp == null || !comp.isUpgrading || !comp.AreUpgradeMaterialsSatisfied;
            });

            // 1. Go to machine interaction cell
            yield return Toils_Goto.GotoThing(MachineInd, PathEndMode.InteractionCell);

            // 2. Work on upgrading the machine
            Toil upgradeWork = ToilMaker.MakeToil("UpgradeAmmoCapacitorWork");
            upgradeWork.tickAction = () =>
            {
                Pawn actor = upgradeWork.actor;
                var comp = Machine?.TryGetComp<CompAmmoCapacitor>();
                if (comp == null || !comp.isUpgrading)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return;
                }

                float speed = Mathf.Max(0.1f, actor.GetStatValue(StatDefOf.WorkSpeedGlobal));
                actor.skills?.Learn(SkillDefOf.Crafting, 0.08f);

                if (comp.DoUpgradeWork(speed, actor))
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };
            upgradeWork.defaultCompleteMode = ToilCompleteMode.Never;
            upgradeWork.WithEffect(EffecterDefOf.ConstructMetal, MachineInd);
            upgradeWork.WithProgressBar(MachineInd, () =>
            {
                var comp = Machine?.TryGetComp<CompAmmoCapacitor>();
                if (comp == null) return 0f;
                return Mathf.Clamp01(comp.upgradeWorkDone / comp.UpgradeWorkTotal);
            });
            upgradeWork.activeSkill = () => SkillDefOf.Crafting;
            yield return upgradeWork;
        }
    }
}
