using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using HarmonyLib;
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
        public bool spawnImpactSmoke = true;
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
        public float airburstSmokeScale = 1.0f;
        public int airburstSmokeCount = 22;
        public float airburstSmokeConeLength = 4.5f;
        public float airburstSmokeConeAngle = -1f; // <= 0 uses fragXZAngleRange half-span
        public float airburstSmokeRadius = 3.0f;   // Circular radius for indirect fire airburst smoke
        public FleckDef shrapnelSmokeFleck;
        public float shrapnelSmokeScale = 1.0f;
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

        internal struct ShrapnelSmokeConfig
        {
            public FleckDef fleckDef;
            public float scale;
        }

        // Tracks airburst shrapnel projectiles by thingIDNumber so they spawn lingering wind smoke on ground impact
        private static readonly Dictionary<int, ShrapnelSmokeConfig> _trackedShrapnel = new Dictionary<int, ShrapnelSmokeConfig>();

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

        public void Throw(Vector3 pos, Map map, Thing instigator, Vector3 velocityVector, float fallbackShotRotation, float scaleFactor = 1f, Vector3? screenDrawPos = null)
        {
            if (Props == null || Props.fragments.NullOrEmpty()) return;
            if (map == null || !pos.ToIntVec3().InBounds(map)) return;

            // Compute screenspace coordinate where the shell visually explodes (DrawPos instead of ground XZ)
            Vector3 screenPos = screenDrawPos ?? (parent != null ? parent.DrawPos : pos);

            // Determine whether this shell is in indirect fire mode or direct fire mode
            var airburstExt = parent?.def?.GetModExtension<AirburstExtension>();
            bool isIndirect = (airburstExt != null && airburstExt.type == AirburstType.Altitude)
                              || (parent?.def?.projectile != null && parent.def.projectile.flyOverhead);

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

            AMCLogger.LogAirburst($"═══ DETONATION: {shellDefName} [{shellThingID}] (Mode: {(isIndirect ? "Indirect" : "Direct")}) ═══");
            AMCLogger.LogAirburst($"Location: {pos} | ScreenPos: {screenPos} | Velocity: {velocityVector} (Speed: {velocityVector.magnitude:F1}, Pitch: {flightPitch:F1}°, Yaw: {flightYaw:F1}°) | Total Fragments Scheduled: {totalScheduled}");

            // --- MID-AIR DETONATION VISUAL & SOUND EFFECTS (at screenspace explosion coordinate) ---
            try
            {
                IntVec3 cellPos = pos.ToIntVec3();
                IntVec3 screenCell = screenPos.ToIntVec3();
                screenCell.x = Mathf.Clamp(screenCell.x, 0, map.Size.x - 1);
                screenCell.z = Mathf.Clamp(screenCell.z, 0, map.Size.z - 1);

                // 1. Play Sound
                SoundDef soundToPlay = Props.airburstSound ?? parent?.def?.projectile?.soundExplode ?? SoundDef.Named("Explosion_Bomb");
                if (soundToPlay != null)
                {
                    soundToPlay.PlayOneShot(new TargetInfo(cellPos, map));
                }

                // 2. Airburst Flash Effecter (AMC_MuzzleFlashLight) at screenspace explosion cell
                EffecterDef flashEffecterDef = Props.airburstFlashEffect ?? DefDatabase<EffecterDef>.GetNamedSilentFail("AMC_MuzzleFlashLight");
                if (flashEffecterDef != null)
                {
                    TargetInfo targetInfo = new TargetInfo(screenCell, map, false);
                    Effecter eff = flashEffecterDef.Spawn();
                    eff.Trigger(targetInfo, targetInfo);
                    eff.Cleanup();
                }

                // 3. Additional Flash Fleck at screenspace explosion coordinate
                FleckDef flashDef = Props.airburstFlashFleck ?? DefDatabase<FleckDef>.GetNamedSilentFail("AMC_MuzzleFlash") ?? FleckDefOf.ExplosionFlash;
                if (flashDef != null && Props.airburstFlashScale > 0f)
                {
                    FleckCreationData flashData = FleckMaker.GetDataStatic(screenPos, map, flashDef);
                    flashData.scale = Props.airburstFlashScale;
                    flashData.rotation = Rand.Range(0f, 360f);
                    map.flecks.CreateFleck(flashData);
                }

                // 4. Airburst Main Explosion Smoke at screenspace explosion coordinate:
                // - Direct fire mode: Random distribution in a cone originating from screenPos
                // - Indirect fire mode: Random circular distribution around screenPos
                SpawnAirburstExplosionSmoke(screenPos, pos, map, velocityVector, fallbackShotRotation, isIndirect);

                // 5. Custom Effecter if configured
                if (Props.airburstEffecter != null)
                {
                    TargetInfo targetInfo = new TargetInfo(screenCell, map, false);
                    Effecter eff = Props.airburstEffecter.Spawn();
                    eff.Trigger(targetInfo, targetInfo);
                    eff.Cleanup();
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

        /// <summary>
        /// Spawns main airburst explosion smoke starting at the screenspace explosion coordinate (DrawPos):
        /// - Direct fire: Random distribution in a forward cone along the 2D screen flight direction.
        /// - Indirect fire: Random circular distribution around the explosion coordinate.
        /// </summary>
        private void SpawnAirburstExplosionSmoke(
            Vector3 screenPos,
            Vector3 groundPos,
            Map map,
            Vector3 velocityVector,
            float fallbackShotRotation,
            bool isIndirect)
        {
            if (Props.airburstSmokeScale <= 0f || Props.airburstSmokeCount <= 0) return;
            if (!groundPos.ShouldSpawnMotesAt(map)) return;

            FleckDef smokeDef = Props.airburstSmokeFleck
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("AMC_AirburstSmoke")
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("AMC_HeatSmoke")
                ?? FleckDefOf.Smoke;
            if (smokeDef == null) return;

            var smokeManager = map.GetComponent<MapComponent_TurretSmokeManager>();
            int smokeCount = Props.airburstSmokeCount;
            float baseScale = Props.airburstSmokeScale;

            if (!isIndirect)
            {
                // DIRECT FIRE MODE: Random distribution in a cone coming from screenPos
                Vector3 screenFlightDir = new Vector3(velocityVector.x, 0f, velocityVector.z + (velocityVector.y * 0.84f));
                if (screenFlightDir.sqrMagnitude > 0.0001f)
                {
                    screenFlightDir.Normalize();
                }
                else
                {
                    // Fallback to CE shotRotation (counter-clockwise from North)
                    float rotRad = fallbackShotRotation * Mathf.Deg2Rad;
                    screenFlightDir = new Vector3(-Mathf.Sin(rotRad), 0f, Mathf.Cos(rotRad)).normalized;
                }

                // RimWorld clockwise angle from North (+Z)
                float forwardAngleDeg = Mathf.Atan2(screenFlightDir.x, screenFlightDir.z) * Mathf.Rad2Deg;
                float halfConeDeg = Props.airburstSmokeConeAngle > 0f
                    ? Props.airburstSmokeConeAngle
                    : Mathf.Clamp(Mathf.Abs(Props.fragXZAngleRange.max - Props.fragXZAngleRange.min) * 0.5f, 15f, 60f);
                float coneLength = Props.airburstSmokeConeLength * Mathf.Max(0.5f, baseScale);

                for (int i = 0; i < smokeCount; i++)
                {
                    float relAngle = Rand.Range(-halfConeDeg, halfConeDeg);
                    float puffAngleDeg = forwardAngleDeg + relAngle;
                    float puffAngleRad = puffAngleDeg * Mathf.Deg2Rad;
                    Vector3 coneDir = new Vector3(Mathf.Sin(puffAngleRad), 0f, Mathf.Cos(puffAngleRad));

                    // Random distance along cone from screenspace explosion apex
                    float t = Mathf.Pow(Rand.Value, 0.7f);
                    float dist = t * coneLength;

                    Vector3 apexJitter = new Vector3(Rand.Range(-0.15f, 0.15f), 0f, Rand.Range(-0.15f, 0.15f));
                    Vector3 puffPos = screenPos + (coneDir * dist) + apexJitter;

                    // Combine map wind drift with slight forward cone expansion velocity
                    Vector3 windVel = smokeManager != null ? smokeManager.GetWindVelocityAt(groundPos) : new Vector3(0.25f, 0f, 0.25f);
                    Vector3 totalVel = windVel + (coneDir * Rand.Range(0.15f, 0.55f));

                    float velAngle = Mathf.Atan2(totalVel.x, totalVel.z) * Mathf.Rad2Deg;
                    float velSpeed = totalVel.magnitude;
                    float puffScale = baseScale * Rand.Range(0.85f, 1.55f) * Mathf.Lerp(0.85f, 1.25f, t);

                    FleckCreationData smokeData = FleckMaker.GetDataStatic(puffPos, map, smokeDef, puffScale);
                    smokeData.rotation = Rand.Range(0f, 360f);
                    smokeData.rotationRate = Rand.Range(-25f, 25f);
                    smokeData.velocityAngle = velAngle;
                    smokeData.velocitySpeed = velSpeed;
                    smokeData.solidTimeOverride = smokeDef.solidTime * Rand.Range(0.85f, 1.25f);
                    map.flecks.CreateFleck(smokeData);
                }
            }
            else
            {
                // INDIRECT FIRE MODE: Circular random distribution around the screenspace explosion location
                float radius = Props.airburstSmokeRadius * Mathf.Max(0.5f, baseScale);

                for (int i = 0; i < smokeCount; i++)
                {
                    float r = Mathf.Sqrt(Rand.Value) * radius;
                    float angleDeg = Rand.Range(0f, 360f);
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    Vector3 radialDir = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));

                    Vector3 puffPos = screenPos + (radialDir * r);

                    // Combine map wind drift with gentle radial expansion
                    Vector3 windVel = smokeManager != null ? smokeManager.GetWindVelocityAt(groundPos) : new Vector3(0.25f, 0f, 0.25f);
                    Vector3 totalVel = windVel + (radialDir * Rand.Range(0.10f, 0.35f));

                    float velAngle = Mathf.Atan2(totalVel.x, totalVel.z) * Mathf.Rad2Deg;
                    float velSpeed = totalVel.magnitude;
                    float puffScale = baseScale * Rand.Range(0.90f, 1.65f);

                    FleckCreationData smokeData = FleckMaker.GetDataStatic(puffPos, map, smokeDef, puffScale);
                    smokeData.rotation = Rand.Range(0f, 360f);
                    smokeData.rotationRate = Rand.Range(-25f, 25f);
                    smokeData.velocityAngle = velAngle;
                    smokeData.velocitySpeed = velSpeed;
                    smokeData.solidTimeOverride = smokeDef.solidTime * Rand.Range(0.85f, 1.25f);
                    map.flecks.CreateFleck(smokeData);
                }
            }
        }

        internal static void RegisterShrapnelProjectile(ProjectileCE projectile, FleckDef shrapnelSmokeFleck, float shrapnelSmokeScale)
        {
            if (projectile == null) return;
            _trackedShrapnel[projectile.thingIDNumber] = new ShrapnelSmokeConfig
            {
                fleckDef = shrapnelSmokeFleck,
                scale = shrapnelSmokeScale
            };
        }

        internal static void TrySpawnShrapnelGroundSmoke(ProjectileCE projectile)
        {
            if (projectile == null) return;

            bool isTracked = _trackedShrapnel.TryGetValue(projectile.thingIDNumber, out ShrapnelSmokeConfig config);
            if (isTracked)
            {
                _trackedShrapnel.Remove(projectile.thingIDNumber);
            }

            string defName = projectile.def?.defName;
            bool isAirburstFragDef = defName == "Airburst_Fragment" || defName == "Airburst_Fragment_Large";
            if (!isTracked && !isAirburstFragDef)
            {
                return;
            }

            Map map = projectile.Map;
            if (map == null) return;

            Vector3 exactPos = projectile.ExactPosition;
            // Ensure this is a ground/surface impact (not a high-altitude detonation)
            if (exactPos.y > 2.0f) return;

            Vector3 groundImpactPos = new Vector3(
                exactPos.x + Rand.Range(-0.12f, 0.12f),
                0f,
                exactPos.z + Rand.Range(-0.12f, 0.12f));

            if (!groundImpactPos.ShouldSpawnMotesAt(map)) return;

            FleckDef smokeDef = config.fleckDef
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("AMC_ShrapnelSmoke")
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("AMC_HeatSmoke")
                ?? FleckDefOf.Smoke;
            if (smokeDef == null) return;

            float baseScale = config.scale > 0f ? config.scale : 1.0f;
            if (defName == "Airburst_Fragment_Large")
            {
                baseScale *= 1.25f;
            }
            else if (defName == "Fragment_Small")
            {
                baseScale *= 0.8f;
            }

            float finalScale = baseScale * Rand.Range(0.85f, 1.35f);

            var smokeManager = map.GetComponent<MapComponent_TurretSmokeManager>();
            Vector3 windVel = smokeManager != null
                ? smokeManager.GetWindVelocityAt(groundImpactPos)
                : new Vector3(0.25f, 0f, 0.25f);

            float velAngle = Mathf.Atan2(windVel.x, windVel.z) * Mathf.Rad2Deg;
            float velSpeed = windVel.magnitude;

            FleckCreationData data = FleckMaker.GetDataStatic(groundImpactPos, map, smokeDef, finalScale);
            data.rotation = Rand.Range(0f, 360f);
            data.rotationRate = Rand.Range(-25f, 25f);
            data.velocityAngle = velAngle;
            data.velocitySpeed = velSpeed;
            data.solidTimeOverride = smokeDef.solidTime * Rand.Range(0.85f, 1.25f);
            map.flecks.CreateFleck(data);
        }

        internal static void UnregisterShrapnelProjectile(int thingIDNumber)
        {
            _trackedShrapnel.Remove(thingIDNumber);
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
                if (spec.spawnImpactSmoke)
                {
                    RegisterShrapnelProjectile(projectile, props.shrapnelSmokeFleck, props.shrapnelSmokeScale);
                }

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

    /// <summary>
    /// Harmony patch on ProjectileCE.Impact to spawn a wind-drifting lingering smoke puff
    /// for each airburst shrapnel that hits the ground (for both direct and indirect fire modes).
    /// </summary>
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Impact))]
    public static class Patch_ProjectileCE_Impact_AirburstShrapnelSmoke
    {
        public static void Prefix(ProjectileCE __instance)
        {
            if (__instance == null) return;
            CompAirburstFragments.TrySpawnShrapnelGroundSmoke(__instance);
        }
    }
}
