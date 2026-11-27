using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component attached to manned turrets to make them enclosed.
    /// Provides the dismount gizmo and manages safety conditions.
    /// </summary>
    public class CompEnclosedTurret : ThingComp
    {
        public CompProperties_EnclosedTurret Props => (CompProperties_EnclosedTurret)props;

        public Building Turret => parent as Building;

        public bool IsActive
        {
            get
            {
                if (Turret == null || !Turret.Spawned) return false;
                var mannable = Turret.GetComp<CompMannable>();
                return mannable != null;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (!IsActive || Turret == null) yield break;

            CompMannable mannable = Turret.GetComp<CompMannable>();
            if (mannable != null && mannable.MannedNow && mannable.ManningPawn != null)
            {
                Pawn operatorPawn = mannable.ManningPawn;

                // Show dismount button if pawn or turret belongs to player
                if (operatorPawn.Faction == Faction.OfPlayer || Turret.Faction == Faction.OfPlayer)
                {
                    Texture2D iconTex = null;
                    if (!string.IsNullOrEmpty(Props.dismountGizmoIcon))
                    {
                        iconTex = ContentFinder<Texture2D>.Get(Props.dismountGizmoIcon, false);
                    }
                    if (iconTex == null)
                    {
                        iconTex = ContentFinder<Texture2D>.Get("UI/Commands/Deselect", true);
                    }

                    yield return new Command_Action
                    {
                        defaultLabel = Props.dismountGizmoLabel,
                        defaultDesc = Props.dismountGizmoDesc,
                        icon = iconTex,
                        action = () => EjectOperator(operatorPawn)
                    };
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (Props.HasSeat)
            {
                OpenTopSeatUtility.Register(this);
            }
        }

        // No PostDeSpawn override: the mod compiles against the 1.5 reference (PostDeSpawn(Map)) but runs on
        // 1.7 (PostDeSpawn(Map, DestroyMode)), so an override would never be called. The seat registry prunes
        // despawned turrets itself (see OpenTopSeatUtility.SeatedTurrets).

        public override void CompTick()
        {
            base.CompTick();

            // Open-top seat: the gunner faces the barrel while seated.
            if (Props.HasSeat)
            {
                Pawn gunner = OpenTopSeatUtility.SeatedGunner(this);
                if (gunner != null)
                {
                    gunner.Rotation = Rot4.FromAngleFlat(OpenTopSeatUtility.TopAngle(Turret));
                }
            }

            if (!parent.IsHashIntervalTick(30)) return;

            CompMannable mannable = Turret?.GetComp<CompMannable>();
            if (mannable != null && mannable.MannedNow && mannable.ManningPawn != null)
            {
                Pawn pawn = mannable.ManningPawn;
                if (pawn.Dead || pawn.Downed)
                {
                    EjectOperator(pawn);
                }
            }
        }

        public void EjectOperator(Pawn pawn)
        {
            if (pawn == null) return;

            // Clear prioritized work order if active
            if (pawn.mindState != null && pawn.mindState.priorityWork != null)
            {
                pawn.mindState.priorityWork.Clear();
            }

            // Clear queued jobs & end current job
            if (pawn.jobs != null)
            {
                pawn.jobs.ClearQueuedJobs();
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            }
        }
    }

    /// <summary>
    /// Helper extension methods for checking enclosed turret status on pawns.
    /// </summary>
    public static class EnclosedTurretUtility
    {
        public static bool IsManningEnclosedTurret(this Pawn pawn, out Building turret, out CompEnclosedTurret comp)
        {
            turret = null;
            comp = null;

            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.CurJobDef != JobDefOf.ManTurret)
                return false;

            turret = pawn.CurJob?.targetA.Thing as Building;
            if (turret == null || !turret.Spawned)
                return false;

            // Must be mannable and pawn must actively be at interaction spot manning it
            var mannableComp = turret.GetComp<CompMannable>();
            if (mannableComp == null || mannableComp.ManningPawn != pawn)
                return false;

            comp = turret.GetComp<CompEnclosedTurret>();
            return comp != null && comp.IsActive;
        }
    }
}
