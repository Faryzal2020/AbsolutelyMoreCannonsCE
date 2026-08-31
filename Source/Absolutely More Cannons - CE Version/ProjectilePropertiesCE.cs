using CombatExtended;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Extension of Combat Extended's ProjectilePropertiesCE that adds parameters for AMC:
    /// - damageRadius: Applies silent radial suppression/fragment damage on impact.
    /// - guidanceDelay: Delay (in seconds) before rocket guidance / homing steering activates.
    /// - guidanceDelayTicks: Optional direct tick count for guidance activation delay.
    /// - homingAcceleration: Steering rate in radians per tick for guided/homing projectiles.
    /// - vlsLaunchAngle: Optional initial launch angle override (in degrees, e.g. 80-88°) for VLS rockets.
    /// </summary>
    public class ProjectilePropertiesCE : CombatExtended.ProjectilePropertiesCE
    {
        public float damageRadius = 0f;

        public float guidanceDelay = 0f;
        public int guidanceDelayTicks = -1;

        public bool guidanceOnDescending = false;

        public float retargetRadius = 0f;

        public float homingAcceleration = 0f;
        public float vlsLaunchAngle = 0f;

        public int GuidanceDelayTicks => guidanceDelayTicks >= 0 ? guidanceDelayTicks : Mathf.RoundToInt(guidanceDelay * GenTicks.TicksPerRealSecond);
    }
}
