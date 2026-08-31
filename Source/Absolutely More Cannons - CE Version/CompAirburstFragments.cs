using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using CombatExtended;
using CombatExtended.Compatibility;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Configuration specification for individual fragment types spawned by CompAirburstFragments.
    /// Allows setting custom graphics, damage, penetration, explosion on impact, and speed.
    /// </summary>
    public class AirburstFragmentSpec
    {
        public ThingDef thingDef;
        public int count = 1;
        public int damageAmountBase = -1; // -1 = use thingDef default
        public DamageDef damageDef; // null = use thingDef default
        public float armorPenetrationSharp = -1f; // -1 = use thingDef default
        public float armorPenetrationBlunt = -1f; // -1 = use thingDef default
        public float explosionRadius = 0f; // > 0 = explode on impact
        public DamageDef explosionDamageDef; // null = use damageDef or Bomb
        public float speed = -1f; // -1 = use thingDef speed
    }

    /// <summary>
    /// Component properties for custom 3D trajectory-aligned airburst fragments.
    /// </summary>
    public class CompProperties_AirburstFragments : CompProperties
    {
        public List<AirburstFragmentSpec> fragments = new List<AirburstFragmentSpec>();
        public float fragSpeedFactor = 1f;
        public float fragShadowChance = 0.2f;
        public FloatRange fragAngleRange = new FloatRange(-25f, 25f);   // Pitch angle range in degrees relative to flight vector
        public FloatRange fragXZAngleRange = new FloatRange(-25f, 25f); // Yaw angle range in degrees relative to flight vector
        public bool useEllipticalCone = false;                           // Constrain fragment spread to an elliptical cone
        public bool usePolarDiskSampling = false;                        // If true, uses direct polar disk mapping (Option B); otherwise uses rejection sampling (Option A)

        // Mid-air airburst detonation visual & sound effects
        public SoundDef airburstSound;
        public FleckDef airburstFlashFleck;
        public float airburstFlashScale = 4.0f;
        public FleckDef airburstSmokeFleck;
        public float airburstSmokeScale = 3.0f;
        public EffecterDef airburstFlashEffect;
        public EffecterDef airburstEffecter;

        public CompProperties_AirburstFragments()
        {
            compClass = typeof(CompAirburstFragments);
        }
    }

    /// <summary>
    /// Custom projectile subclass for fragments that support stat overrides (damage, pen, explosion on impact).
    /// </summary>
    public class ProjectileCE_AMCFragment : ProjectileCE
    {
        public float? armorPenetrationSharpOverride;
        public float? armorPenetrationBluntOverride;
        public float explosionRadiusOverride = 0f;
        public DamageDef explosionDamageDefOverride;

        public override float PenetrationAmount
        {
            get
            {
                var isSharpDmg = DamageDef.armorCategory == DamageArmorCategoryDefOf.Sharp;
                if (isSharpDmg && armorPenetrationSharpOverride.HasValue)
                {
                    return armorPenetrationSharpOverride.Value * RemainingSpeedPct;
                }
                if (!isSharpDmg && armorPenetrationBluntOverride.HasValue)
                {
                    return armorPenetrationBluntOverride.Value * RemainingKineticEnergyPct;
                }
                return base.PenetrationAmount;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref armorPenetrationSharpOverride, "armorPenetrationSharpOverride");
            Scribe_Values.Look(ref armorPenetrationBluntOverride, "armorPenetrationBluntOverride");
            Scribe_Values.Look(ref explosionRadiusOverride, "explosionRadiusOverride", 0f);
            Scribe_Defs.Look(ref explosionDamageDefOverride, "explosionDamageDefOverride");
        }

        public override void Impact(Thing hitThing)
        {
            base.Impact(hitThing);

            if (Map != null)
            {
                if (explosionRadiusOverride > 0f)
                {
                    GenExplosionCE.DoExplosion(
                        ExactPosition.ToIntVec3(),
                        Map,
                        explosionRadiusOverride,
                        explosionDamageDefOverride ?? DamageDef ?? DamageDefOf.Bomb,
                        launcher,
                        Mathf.FloorToInt(DamageAmount),
                        PenetrationAmount
                    );
                }

                // Explicitly trigger CompScatteredSparks on fragment impact
                var comp = GetComp<CompScatteredSparks>();
                comp?.OnImpact(hitThing);
            }
        }
    }

    /// <summary>
    /// Custom fragment generator component that computes a true 3D trajectory-aligned cone
    /// relative to the shell's velocity vector, preventing ground projection distortion.
    /// </summary>
    [StaticConstructorOnStartup]
    public class CompAirburstFragments : ThingComp
    {
        private class MonoDummy : MonoBehaviour { }
        private static MonoDummy _monoDummy;

        private static readonly Action<Thing> _tickAction;

        static CompAirburstFragments()
        {
            var dummyGO = new GameObject("AMC_AirburstMonoDummy");
            UnityEngine.Object.DontDestroyOnLoad(dummyGO);
            _monoDummy = dummyGO.AddComponent<MonoDummy>();

            var tickMethod = typeof(Thing).GetMethod("Tick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (tickMethod != null)
            {
                _tickAction = (Action<Thing>)Delegate.CreateDelegate(typeof(Action<Thing>), null, tickMethod);
            }
        }

        public CompProperties_AirburstFragments Props => (CompProperties_AirburstFragments)props;

        public void Throw(Vector3 pos, Map map, Thing instigator, Vector3 velocityVector, float fallbackShotRotation, float scaleFactor = 1f)
        {
            if (Props == null || Props.fragments.NullOrEmpty()) return;
            if (map == null || !pos.ToIntVec3().InBounds(map)) return;

            // Compute 3D shell trajectory rotation
            Quaternion shellRotation;
            if (velocityVector.sqrMagnitude > 0.0001f)
            {
                shellRotation = Quaternion.LookRotation(velocityVector);
            }
            else
            {
                shellRotation = Quaternion.Euler(0, fallbackShotRotation, 0);
            }

            int totalScheduled = 0;
            foreach (var fragSpec in Props.fragments)
            {
                totalScheduled += Mathf.RoundToInt(fragSpec.count * scaleFactor);
            }

            string shellDefName = parent?.def?.defName ?? "AirburstShell";
            string shellThingID = parent?.ThingID ?? "N/A";
            float horizDist = Mathf.Sqrt(velocityVector.x * velocityVector.x + velocityVector.z * velocityVector.z);
            float flightPitch = Mathf.Rad2Deg * Mathf.Atan2(velocityVector.y, horizDist);
            float flightYaw = (-90f + Mathf.Rad2Deg * Mathf.Atan2(velocityVector.z, velocityVector.x)) % 360f;
            if (flightYaw < 0f) flightYaw += 360f;

            AMCLogger.LogAirburst($"═══ DETONATION: {shellDefName} [{shellThingID}] ═══");
            AMCLogger.LogAirburst($"Location: {pos} | Velocity: {velocityVector} (Speed: {velocityVector.magnitude:F1}, Pitch: {flightPitch:F1}°, Yaw: {flightYaw:F1}°) | Total Fragments Scheduled: {totalScheduled}");

            // --- MID-AIR DETONATION VISUAL & SOUND EFFECTS ---
            try
            {
                IntVec3 cellPos = pos.ToIntVec3();

                // 1. Play Sound
                SoundDef soundToPlay = Props.airburstSound ?? parent?.def?.projectile?.soundExplode ?? SoundDef.Named("Explosion_Bomb");
                if (soundToPlay != null)
                {
                    soundToPlay.PlayOneShot(new TargetInfo(cellPos, map));
                }

                // 2. Airburst Flash Effecter (AMC_MuzzleFlashLight)
                EffecterDef flashEffecterDef = Props.airburstFlashEffect ?? DefDatabase<EffecterDef>.GetNamedSilentFail("AMC_MuzzleFlashLight");
                if (flashEffecterDef != null)
                {
                    TargetInfo targetInfo = new TargetInfo(cellPos, map, false);
                    Effecter eff = flashEffecterDef.Spawn();
                    eff.Trigger(targetInfo, targetInfo);
                    eff.Cleanup();
                }

                // 3. Additional Flash Fleck
                FleckDef flashDef = Props.airburstFlashFleck ?? DefDatabase<FleckDef>.GetNamedSilentFail("AMC_MuzzleFlash") ?? FleckDefOf.ExplosionFlash;
                if (flashDef != null && Props.airburstFlashScale > 0f)
                {
                    FleckCreationData flashData = FleckMaker.GetDataStatic(pos, map, flashDef);
                    flashData.scale = Props.airburstFlashScale;
                    flashData.rotation = Rand.Range(0f, 360f);
                    map.flecks.CreateFleck(flashData);
                }

            }
            catch (Exception ex)
            {
                AMCLogger.LogAirburst($"Error spawning airburst FX: {ex}");
            }

            int[] fragCounter = new int[1] { 0 };
            foreach (var fragSpec in Props.fragments)
            {
                int count = Mathf.RoundToInt(fragSpec.count * scaleFactor);
                if (count <= 0) continue;

                var routine = FragRoutine(pos, map, instigator, shellRotation, fragSpec, Props, fragCounter);
                if (!Multiplayer.InMultiplayer && _monoDummy != null)
                {
                    _monoDummy.StartCoroutine(routine);
                }
                else
                {
                    while (routine.MoveNext()) { }
                }
            }
        }

        private static IEnumerator FragRoutine(
            Vector3 pos,
            Map map,
            Thing instigator,
            Quaternion shellRotation,
            AirburstFragmentSpec spec,
            CompProperties_AirburstFragments props,
            int[] fragCounter)
        {
            float height = Mathf.Max(pos.y, 0.001f);
            IntVec3 cell = pos.ToIntVec3();
            Vector2 exactOrigin = new Vector2(pos.x, pos.z);

            ThingDef projDef = spec.thingDef ?? CE_ThingDefOf.Fragment_Large;
            int fragToSpawn = spec.count;
            int fragPerTick = Mathf.Max(1, Mathf.CeilToInt((float)fragToSpawn / 10f));
            int fragSpawnedInTick = 0;

            while (fragToSpawn-- > 0)
            {
                fragCounter[0]++;
                int fragNumber = fragCounter[0];

                var projectile = (ProjectileCE)ThingMaker.MakeThing(projDef);
                GenSpawn.Spawn(projectile, cell, map);

                projectile.canTargetSelf = false;
                projectile.minCollisionDistance = 0.5f;
                projectile.logMisses = false;

                // 1. Pick relative angles in local cone space
                float relPitch;
                float relYaw;

                if (props.useEllipticalCone)
                {
                    float pitchCenter = (props.fragAngleRange.min + props.fragAngleRange.max) * 0.5f;
                    float pitchRadius = Mathf.Abs(props.fragAngleRange.max - props.fragAngleRange.min) * 0.5f;
                    float yawCenter   = (props.fragXZAngleRange.min + props.fragXZAngleRange.max) * 0.5f;
                    float yawRadius   = Mathf.Abs(props.fragXZAngleRange.max - props.fragXZAngleRange.min) * 0.5f;

                    if (props.usePolarDiskSampling)
                    {
                        // Option B: Polar Disk Mapping
                        float r = Mathf.Sqrt(Rand.Value);
                        float beta = Rand.Range(0f, Mathf.PI * 2f);

                        float u = r * Mathf.Cos(beta);
                        float v = r * Mathf.Sin(beta);

                        relPitch = pitchCenter + (v * pitchRadius);
                        relYaw   = yawCenter   + (u * yawRadius);
                    }
                    else
                    {
                        // Option A: Elliptical Rejection Sampling
                        int attempts = 25;
                        do
                        {
                            relPitch = props.fragAngleRange.RandomInRange;
                            relYaw   = props.fragXZAngleRange.RandomInRange;

                            float normPitch = pitchRadius > 0.0001f ? (relPitch - pitchCenter) / pitchRadius : 0f;
                            float normYaw   = yawRadius > 0.0001f ? (relYaw - yawCenter) / yawRadius : 0f;

                            if ((normPitch * normPitch + normYaw * normYaw) <= 1.0f)
                            {
                                break;
                            }
                        } while (--attempts > 0);
                    }
                }
                else
                {
                    // Default Rectangular Cone
                    relPitch = props.fragAngleRange.RandomInRange;
                    relYaw   = props.fragXZAngleRange.RandomInRange;
                }

                // 2. Compute local direction vector (forward is +Z)
                Vector3 localDir = Quaternion.Euler(-relPitch, relYaw, 0f) * Vector3.forward;

                // 3. Rotate local direction into world space using shell's 3D flight rotation
                Vector3 worldDir = shellRotation * localDir;

                // 4. Convert 3D world direction vector to CE Launch parameters (shotAngle, shotRotation)
                float horizMag = Mathf.Sqrt(worldDir.x * worldDir.x + worldDir.z * worldDir.z);
                float shotAngle = Mathf.Atan2(worldDir.y, horizMag); // Radians [-pi/2, pi/2]

                float shotRotation = (-90f + Mathf.Rad2Deg * Mathf.Atan2(worldDir.z, worldDir.x)) % 360f;
                if (shotRotation < 0f) shotRotation += 360f;

                float speed = spec.speed > 0 ? spec.speed : (projectile.def.projectile.speed * props.fragSpeedFactor);

                // 5. Apply Stat Overrides if configured
                if (spec.damageAmountBase >= 0)
                {
                    projectile.DamageAmount = spec.damageAmountBase;
                }
                if (spec.damageDef != null)
                {
                    projectile.damageDefOverride = spec.damageDef;
                }

                if (projectile is ProjectileCE_AMCFragment amcFrag)
                {
                    if (spec.armorPenetrationSharp >= 0) amcFrag.armorPenetrationSharpOverride = spec.armorPenetrationSharp;
                    if (spec.armorPenetrationBlunt >= 0) amcFrag.armorPenetrationBluntOverride = spec.armorPenetrationBlunt;
                    if (spec.explosionRadius > 0)
                    {
                        amcFrag.explosionRadiusOverride = spec.explosionRadius;
                        amcFrag.explosionDamageDefOverride = spec.explosionDamageDef;
                    }
                }

                // 6. Launch Projectile
                projectile.Launch(
                    instigator,
                    exactOrigin,
                    shotAngle,
                    shotRotation,
                    height,
                    speed,
                    projectile
                );

                projectile.castShadow = (Rand.Value < props.fragShadowChance);

                // Telemetry Logging
                float worldPitchDeg = shotAngle * Mathf.Rad2Deg;
                string dmgDefName = projectile.DamageDef?.defName ?? "Default";
                float penAmount = projectile.PenetrationAmount;
                string expInfo = spec.explosionRadius > 0 ? $" | ExplodeRad:{spec.explosionRadius:F1} ({spec.explosionDamageDef?.defName ?? dmgDefName})" : "";

                AMCLogger.LogAirburst($"  ├─ Fragment #{fragNumber} [{projectile.ThingID}] Def:{projDef.defName} | Dmg:{projectile.DamageAmount} ({dmgDefName}), AP:{penAmount:F1}mm, Speed:{speed:F1}{expInfo} | RelAngles:(Pitch={relPitch:F1}°, Yaw={relYaw:F1}°) | World3DAngles:(LaunchAngle={worldPitchDeg:F1}°, LaunchRot={shotRotation:F1}°)");

                if (_tickAction != null)
                {
                    _tickAction(projectile);
                }

                fragSpawnedInTick++;
                if (fragSpawnedInTick >= fragPerTick)
                {
                    fragSpawnedInTick = 0;
                    yield return new WaitForEndOfFrame();
                    if (Find.Maps.IndexOf(map) < 0) break;
                }
            }
        }
    }
}
