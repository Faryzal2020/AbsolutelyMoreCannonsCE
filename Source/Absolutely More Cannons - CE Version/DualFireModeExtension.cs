using System;
using System.Collections.Generic;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// DefModExtension for dual fire mode configuration.
    /// Allows a single turret to support both direct and indirect fire modes.
    /// </summary>
    public class DualFireModeExtension : DefModExtension
    {
        /// <summary>
        /// Direct fire mode configuration.
        /// </summary>
        public FireModeConfig directFire;

        /// <summary>
        /// Indirect fire mode configuration.
        /// </summary>
        public FireModeConfig indirectFire;

        /// <summary>
        /// Default mode when turret is first spawned.
        /// </summary>
        public FireMode defaultMode = FireMode.Direct;

        /// <summary>
        /// Gizmo button icon path for direct mode.
        /// </summary>
        public string directModeIcon = "UI/Buttons/DirectMode";

        /// <summary>
        /// Gizmo button icon path for indirect/artillery mode.
        /// </summary>
        public string indirectModeIcon = "UI/Buttons/ArtyMode";
    }

    /// <summary>
    /// Configuration for a specific fire mode (direct or indirect).
    /// </summary>
    public class FireModeConfig
    {
        /// <summary>
        /// Fully qualified verb class name (e.g., "CombatExtended.Verb_ShootCE" or "CombatExtended.Verb_ShootMortarCE").
        /// </summary>
        public string verbClass;

        /// <summary>
        /// Minimum firing range for this mode.
        /// </summary>
        public float minRange = 0f;

        /// <summary>
        /// Whether line of sight is required to fire.
        /// </summary>
        public bool requireLineOfSight = true;

        /// <summary>
        /// Circular error probability (accuracy spread).
        /// </summary>
        public float circularError = 0f;

        /// <summary>
        /// Indirect fire accuracy penalty.
        /// </summary>
        public float indirectFirePenalty = 0f;

        /// <summary>
        /// Whether to stop burst firing if line of sight is lost.
        /// </summary>
        public bool stopBurstWithoutLos = true;

        /// <summary>
        /// Force normal time speed during firing.
        /// </summary>
        public bool forceNormalTimeSpeed = false;

        /// <summary>
        /// AmmoSet def name for this fire mode.
        /// References existing AmmoSetDef without needing to duplicate ammo.
        /// </summary>
        public string ammoSet;

        /// <summary>
        /// Turret top graphic texture path for this mode.
        /// Different barrel angles for direct vs indirect fire.
        /// </summary>
        public string turretTopGraphic;

        /// <summary>
        /// Optional barrel extension configuration specific to this fire mode.
        /// Allows different recoil/firing animations per mode.
        /// </summary>
        public TurretBarrelExtension barrelExtension;

        /// <summary>
        /// Weapon tags for AI behavior (e.g., Artillery_BaseDestroyer for indirect fire).
        /// </summary>
        public List<string> weaponTags = new List<string>();
    }

    /// <summary>
    /// Fire mode enumeration.
    /// </summary>
    public enum FireMode
    {
        Direct,
        Indirect
    }
}
