using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Per-map list of spawned turrets whose enclosed comp has a seat (open-top turrets).
    /// Auto-instantiated by RimWorld for every map; dies with its map, so no cross-game staleness.
    /// </summary>
    public class MapComponent_OpenTopSeats : MapComponent
    {
        public readonly List<CompEnclosedTurret> turrets = new List<CompEnclosedTurret>();

        public MapComponent_OpenTopSeats(Map map) : base(map) { }
    }

    /// <summary>
    /// Runtime helpers for open-top turret seats: who is seated, where the seat is, how tall the gunner is.
    /// </summary>
    public static class OpenTopSeatUtility
    {
        private static Map cachedMap;
        private static MapComponent_OpenTopSeats cachedComp;
        private static readonly List<CompEnclosedTurret> Empty = new List<CompEnclosedTurret>();

        private static MapComponent_OpenTopSeats SeatsFor(Map map)
        {
            if (map == null) return null;
            if (map != cachedMap || cachedComp == null)
            {
                cachedMap = map;
                cachedComp = map.GetComponent<MapComponent_OpenTopSeats>();
            }
            return cachedComp;
        }

        public static void Register(CompEnclosedTurret comp)
        {
            var seats = SeatsFor(comp?.parent?.Map);
            if (seats != null && !seats.turrets.Contains(comp))
            {
                seats.turrets.Add(comp);
            }
        }

        /// <summary>Spawned seat turrets on this map. Prunes despawned/moved entries as it goes.</summary>
        public static IReadOnlyList<CompEnclosedTurret> SeatedTurrets(Map map)
        {
            var seats = SeatsFor(map);
            if (seats == null) return Empty;
            var list = seats.turrets;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                if (c == null || c.parent == null || !c.parent.Spawned || c.parent.Map != map)
                {
                    list.RemoveAt(i);
                }
            }
            return list;
        }

        /// <summary>True when the pawn is actively manning an enclosed turret that has a seat.</summary>
        public static bool TryGetSeated(Pawn pawn, out Building turret, out CompEnclosedTurret comp)
        {
            if (pawn.IsManningEnclosedTurret(out turret, out comp) && comp.Props.HasSeat)
            {
                return true;
            }
            turret = null;
            comp = null;
            return false;
        }

        /// <summary>The manning pawn of a seat turret, or null if nobody is seated.</summary>
        public static Pawn SeatedGunner(CompEnclosedTurret comp)
        {
            Pawn pawn = comp?.Turret?.GetComp<CompMannable>()?.ManningPawn;
            if (pawn != null && TryGetSeated(pawn, out Building turret, out _) && turret == comp.parent)
            {
                return pawn;
            }
            return null;
        }

        /// <summary>Compass angle of the turret top (0 = North, clockwise).</summary>
        public static float TopAngle(Building turret)
        {
            if (turret is Building_Turret bt)
            {
                float auth = TurretTrackingUtility.GetAuthoritativeBarrelRotation(bt);
                if (!float.IsNaN(auth)) return auth;
            }
            var barrel = turret?.TryGetComp<CompTurretBarrel>();
            if (barrel != null)
            {
                return barrel.GetCurrentBarrelRotation();
            }
            return 0f;
        }

        /// <summary>World position of the seat (turret centre + rotated seatOffset), keeping turret.DrawPos.y.</summary>
        public static Vector3 SeatWorldPos(Building turret, CompEnclosedTurret comp)
        {
            Vector3 center = turret.DrawPos;
            Vector3 off = comp.Props.seatOffset;
            OpenTopGeometry.RotateOffset(off.x, off.z, TopAngle(turret), out float rx, out float rz);
            return new Vector3(center.x + rx, center.y, center.z + rz);
        }

        public static IntVec3 SeatCell(Building turret, CompEnclosedTurret comp)
        {
            return SeatWorldPos(turret, comp).ToIntVec3();
        }

        /// <summary>Seated gunner's CE vertical range: head at barrel height (or 1.45), down by body height.</summary>
        public static FloatRange GunnerVerticalRange(Pawn gunner, Building turret)
        {
            float pawnHeight = CE_Utility.GetCollisionBodyFactors(gunner).y;
            // barrelHeight is the barrel pivot (trunnion) height; the gunner's head sits at that level.
            OpenTopGeometry.GunnerVerticalRange(CompTurretBarrel.GetCustomBarrelHeight(turret), pawnHeight, out float bottom, out float top);
            return new FloatRange(bottom, top);
        }
    }
}
