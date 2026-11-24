using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Defines how a turret's elevation and traverse are actuated.
    /// - AutoDetect: Automatically infers MachineActuated if the turret has CompPowerTrader, CompTurretFCS, or NonSnapTurretExtension; otherwise DirectMechanical.
    /// - MachineActuated: Controlled via terminal / powered servo / hydraulic actuators. Operator pawn skill has zero effect on physical mount recoil.
    /// - DirectMechanical: Hand-cranked, pintle-mounted, or direct mechanical linkage. Operator pawn ShootingAccuracy helps dampen recoil kick.
    /// </summary>
    public enum TurretControlMode
    {
        AutoDetect = 0,
        MachineActuated = 1,
        DirectMechanical = 2
    }

    /// <summary>
    /// Optional DefModExtension (can be placed on either the turret building ThingDef or turret weapon ThingDef)
    /// to fine-tune mounted turret recoil physics, single-pulse impulse response, and actuator stabilization.
    /// Note: All AMC turrets automatically use the mounted recoil system even without this extension.
    /// </summary>
    public class TurretRecoilExtension : DefModExtension
    {
        /// <summary>
        /// How the turret is controlled (AutoDetect, MachineActuated, or DirectMechanical).
        /// MachineActuated turrets completely ignore the operator pawn's ShootingAccuracy for recoil calculation.
        /// </summary>
        public TurretControlMode controlMode = TurretControlMode.AutoDetect;

        /// <summary>
        /// Strength of the turret's hydraulic/electric actuators or mount bracing (default 1.0).
        /// Higher values (> 1.0) recover from the initial firing impulse faster, dampen steady-state vibration more tightly,
        /// and provide stronger torque to accelerate/decelerate turret rotation.
        /// </summary>
        public float actuatorStrength = 1.0f;

        /// <summary>
        /// Rotational inertia / weight of the turret head and gun assembly (default -1 uses TurretBarrelExtension.turretHeadWeight or auto-scales by turret size).
        /// Higher values make the turret accelerate and decelerate more slowly when slewing toward NonSnapTurretExtension.speed.
        /// </summary>
        public float turretHeadWeight = -1f;

        /// <summary>
        /// Peak vertical angular multiplier of the initial firing jolt (up -> down -> tiny up -> stabilized).
        /// Scaled by the weapon's recoilAmount and 1 / sqrt(actuatorStrength).
        /// </summary>
        public float initialJoltMultiplier = 1.0f;

        /// <summary>
        /// Base number of shots (at actuatorStrength = 1.0) required for the initial firing jolt pulse
        /// (up, down, tiny up, center) to fully settle into steady-state equilibrium.
        /// Actual settle shots = Round(baseSettleShots / actuatorStrength).
        /// </summary>
        public int baseSettleShots = 6;

        /// <summary>
        /// Multiplier applied to recoilAmount once the burst has settled into steady-state harmonic vibration.
        /// Divided by actuatorStrength.
        /// </summary>
        public float steadyStateVibration = 0.25f;

        /// <summary>
        /// Ratio of vertical steady-state vibration relative to horizontal steady-state vibration.
        /// Mounted trunnions constrain vertical pitch tightly; default 0.5 keeps vertical vibration symmetric and compact.
        /// </summary>
        public float verticalVibrationRatio = 0.5f;

        /// <summary>
        /// Angular smoothing factor [0..1] between consecutive shots in steady state.
        /// Higher values (e.g. 0.65) smooth out frame-to-frame white noise on ultra-high-RPM rotary guns
        /// so the stream vibrates organically around center rather than teleporting wildly each tick.
        /// </summary>
        public float angularSmoothing = 0.60f;

        /// <summary>
        /// Maximum recoil penalty multiplier from a low-skill operator when controlMode is DirectMechanical.
        /// At ShootingAccuracy = 4.5 (legendary shooter), penalty is 1.0x.
        /// At ShootingAccuracy = 0.5 (unskilled shooter), penalty reaches (1.0 + directControlMaxSkillPenalty), e.g. 1.75x
        /// instead of CE's default 9.19x infantry shoulder-rifle explosion.
        /// </summary>
        public float directControlMaxSkillPenalty = 0.75f;
    }
}
