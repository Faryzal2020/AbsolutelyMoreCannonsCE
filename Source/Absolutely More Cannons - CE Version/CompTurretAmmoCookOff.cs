using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component that gives turrets with an internal magazine (CompAmmoUser) a chance to suffer
    /// an ammunition cook-off explosion when damaged by fire, high-penetration projectiles, or heavy blasts.
    /// Explosion radius and damage scale linearly with the ratio of loaded ammo (100% radius & damage at 100% ammo).
    /// </summary>
    public class CompTurretAmmoCookOff : ThingComp
    {
        private bool cookedOff;

        // Pre-damage snapshot so lethal hits that destroy the turret mid-TakeDamage preserve map, position, and ammo count
        private Map cachedMap;
        private IntVec3 cachedPosition = IntVec3.Invalid;
        private int cachedCurMagCount;
        private int cachedMagSize;
        private ThingDef cachedAmmoDef;
        private int cachedSnapshotTick = -1;

        public CompProperties_TurretAmmoCookOff Props => (CompProperties_TurretAmmoCookOff)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cookedOff, "amcCookedOff", false);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (cookedOff || parent == null || !parent.Spawned)
                return;

            cachedMap = parent.Map;
            cachedPosition = parent.Position;
            cachedSnapshotTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;

            if (TryGetAmmoState(out CompAmmoUser ammoComp, out int curMag, out int magSize))
            {
                cachedCurMagCount = curMag;
                cachedMagSize = magSize;
                cachedAmmoDef = ammoComp.CurrentAmmo;
            }
            else
            {
                cachedCurMagCount = 0;
                cachedMagSize = 0;
                cachedAmmoDef = null;
            }
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            if (cookedOff || totalDamageDealt <= 0f || Props == null)
                return;

            bool isLethal = parent.Destroyed || parent.HitPoints <= 0;
            float hpPercent = parent.MaxHitPoints > 0
                ? Mathf.Clamp01((float)parent.HitPoints / parent.MaxHitPoints)
                : 0f;

            // Only roll on non-lethal hits if HP has dropped to or below hitPointsPercentThreshold
            if (!isLethal && hpPercent > Props.hitPointsPercentThreshold)
                return;

            CompAmmoUser ammoComp = null;
            int curMag = 0;
            int magSize = 0;

            if (TryGetAmmoState(out ammoComp, out int liveCurMag, out int liveMagSize) && liveCurMag > 0)
            {
                curMag = liveCurMag;
                magSize = liveMagSize;
            }
            else
            {
                int nowTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
                if (cachedSnapshotTick == nowTick && cachedCurMagCount > 0 && cachedMagSize > 0)
                {
                    curMag = cachedCurMagCount;
                    magSize = cachedMagSize;
                }
            }

            if (magSize < Props.minMagazineSize || curMag <= 0)
                return;

            float ammoRatio = Mathf.Clamp01((float)curMag / magSize);
            if (ammoRatio < Props.minAmmoRatio)
                return;

            float chance = Props.GetCookOffChance(dinfo, totalDamageDealt, ammoRatio, hpPercent, isLethal);
            if (chance <= 0f)
                return;

            if (Rand.Chance(chance))
            {
                Map map = parent.Map ?? cachedMap;
                IntVec3 pos = parent.Position.IsValid ? parent.Position : cachedPosition;
                TriggerCookOff(map, pos, ammoComp, curMag, magSize, dinfo.Instigator, dinfo.Def?.label ?? "damage");
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (cookedOff || parent == null || !parent.Spawned || Props == null)
                return;

            // Check every 250 ticks (~4.16 seconds) if the turret is actively on fire
            if (!parent.IsHashIntervalTick(250))
                return;

            if (!parent.IsBurning())
                return;

            if (!TryGetAmmoState(out CompAmmoUser ammoComp, out int curMag, out int magSize))
                return;

            if (magSize < Props.minMagazineSize || curMag <= 0)
                return;

            float ammoRatio = Mathf.Clamp01((float)curMag / magSize);
            if (ammoRatio < Props.minAmmoRatio)
                return;

            float hpPercent = parent.MaxHitPoints > 0
                ? Mathf.Clamp01((float)parent.HitPoints / parent.MaxHitPoints)
                : 1f;

            float chance = Props.GetBurningTickCookOffChance(ammoRatio, hpPercent);
            if (chance > 0f && Rand.Chance(chance))
            {
                TriggerCookOff(parent.Map, parent.Position, ammoComp, curMag, magSize, null, "fire");
            }
        }

        private bool TryGetAmmoState(out CompAmmoUser ammoComp, out int curMagCount, out int magSize)
        {
            ammoComp = null;
            curMagCount = 0;
            magSize = 0;

            if (parent == null)
                return false;

            Thing gunThing = (parent as Building_TurretGunCE)?.Gun ?? (parent as Building_TurretGun)?.gun ?? parent;
            ammoComp = (gunThing as ThingWithComps)?.TryGetComp<CompAmmoUser>();
            if (ammoComp == null)
                return false;

            magSize = ammoComp.MagSize;
            curMagCount = ammoComp.CurMagCount;
            return magSize > 0;
        }

        private void TriggerCookOff(Map map, IntVec3 pos, CompAmmoUser ammoComp, int curMag, int magSize, Thing instigator, string causeLabel)
        {
            if (cookedOff || map == null || !pos.IsValid || magSize <= 0 || curMag <= 0)
                return;

            cookedOff = true;

            ThingDef loadedAmmoDef = ammoComp?.CurrentAmmo ?? cachedAmmoDef;

            // Empty internal magazine so it cannot cook off again or drop intact ammo on destruction
            if (ammoComp != null)
            {
                ammoComp.CurMagCount = 0;
            }

            // If a lethal hit already caused the turret to spill its magazine stack onto the ground this tick, remove that spilled stack
            if (parent.Destroyed && loadedAmmoDef != null)
            {
                RemoveSpilledAmmoAround(map, pos, loadedAmmoDef, curMag);
            }

            float ammoRatio = Mathf.Clamp01((float)curMag / magSize);
            float scaledRadius = Mathf.Clamp(Props.maxExplosiveRadius * ammoRatio, Props.minExplosiveRadius, Props.maxExplosiveRadius);
            int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(Props.maxDamage * ammoRatio));
            float armorPen = Props.armorPenetration >= 0f ? Props.armorPenetration * ammoRatio : scaledDamage * 0.15f;
            DamageDef explosionDamageDef = Props.damageDef ?? DamageDefOf.Bomb;

            MoteMaker.ThrowText(pos.ToVector3Shifted(), map, $"Ammo Cook-Off! ({Mathf.RoundToInt(ammoRatio * 100f)}%)", new Color(1f, 0.45f, 0.1f), 4f);

            if (parent.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{parent.LabelCap} suffered an internal magazine cook-off ({curMag}/{magSize} rounds, {scaledRadius:F1}m radius)!",
                    new TargetInfo(pos, map),
                    MessageTypeDefOf.NegativeEvent
                );
            }

            AMCLogger.LogTurretAmmo(
                $"[COOK-OFF] {parent.LabelCap} @ {pos} cooked off from {causeLabel}! " +
                $"Ammo: {curMag}/{magSize} ({ammoRatio:P0}) | Radius: {scaledRadius:F2}/{Props.maxExplosiveRadius:F2} | Damage: {scaledDamage}/{Props.maxDamage}"
            );

            GenExplosionCE.DoExplosion(
                center: pos,
                map: map,
                radius: scaledRadius,
                damType: explosionDamageDef,
                instigator: instigator ?? parent,
                damAmount: scaledDamage,
                armorPenetration: armorPen,
                explosionSound: null,
                weapon: parent.def,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 0,
                postExplosionGasType: null,
                applyDamageToExplosionCellsNeighbors: true,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 0,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: Props.damageFalloff,
                height: 0f
            );

            if (Props.destroyTurretOnCookOff && !parent.Destroyed)
            {
                parent.Destroy(DestroyMode.KillFinalize);
            }
        }

        private static void RemoveSpilledAmmoAround(Map map, IntVec3 center, ThingDef ammoDef, int maxToRemove)
        {
            int remaining = maxToRemove;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 2f, true))
            {
                if (!cell.InBounds(map)) continue;
                List<Thing> thingList = cell.GetThingList(map);
                for (int i = thingList.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    Thing t = thingList[i];
                    if (t != null && !t.Destroyed && t.def == ammoDef)
                    {
                        if (t.stackCount <= remaining)
                        {
                            remaining -= t.stackCount;
                            t.Destroy(DestroyMode.Vanish);
                        }
                        else
                        {
                            t.stackCount -= remaining;
                            remaining = 0;
                        }
                    }
                }
                if (remaining <= 0) break;
            }
        }
    }
}
