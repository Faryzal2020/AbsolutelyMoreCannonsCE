using System;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Unity-free math for open-top turret seats and gun shields.
    /// Kept free of Verse/Unity references so it can be unit-tested by Source/OpenTopGeometry.Tests.
    ///
    /// Angles are compass degrees, clockwise: 0 = North (+Z), 90 = East (+X).
    /// Seat offsets use the turret-top-local frame: +Z along the barrel, +X to the barrel's right.
    /// </summary>
    public static class OpenTopGeometry
    {
        /// <summary>Gunner hitbox top (head height) when the turret has no barrelHeight.</summary>
        public const float DefaultGunnerTop = 1.45f;

        private const double Deg2Rad = Math.PI / 180.0;

        /// <summary>
        /// Rotates a turret-top-local (x,z) offset by the compass turret angle.
        /// x' = x*cos + z*sin, z' = -x*sin + z*cos (matches TextureInspector rotateVecXZ).
        /// </summary>
        public static void RotateOffset(float x, float z, float angleDeg, out float rx, out float rz)
        {
            double rad = angleDeg * Deg2Rad;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            rx = (float)(x * cos + z * sin);
            rz = (float)(-x * sin + z * cos);
        }

        /// <summary>Compass angle of a horizontal direction, in [0,360).</summary>
        public static float CompassAngle(float dx, float dz)
        {
            double deg = Math.Atan2(dx, dz) / Deg2Rad;
            if (deg < 0) deg += 360.0;
            if (deg >= 360.0) deg -= 360.0;
            return (float)deg;
        }

        /// <summary>Smallest absolute difference between two compass angles, in [0,180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (a - b) % 360f;
            if (d < 0) d += 360f;
            return d > 180f ? 360f - d : d;
        }

        /// <summary>
        /// True when a shot travelling along (shotDirX, shotDirZ) arrives from inside the shield arc
        /// centred on the turret-top facing. Arc &lt;= 0 means no shield; &gt;= 360 means all round.
        /// </summary>
        public static bool InShieldArc(float shotDirX, float shotDirZ, float topAngleDeg, float shieldArc)
        {
            if (shieldArc <= 0f) return false;
            if (shieldArc >= 360f) return true;
            float fromAngle = CompassAngle(-shotDirX, -shotDirZ);
            return DeltaAngle(fromAngle, topAngleDeg) <= shieldArc / 2f + 1e-3f;
        }

        /// <summary>
        /// Vertical extent of a seated gunner: head at barrel height (or DefaultGunnerTop),
        /// extending down by the pawn's collision height, clamped at the ground.
        /// </summary>
        public static void GunnerVerticalRange(float barrelHeight, float pawnHeight, out float bottom, out float top)
        {
            top = barrelHeight > 0f ? barrelHeight : DefaultGunnerTop;
            bottom = Math.Max(0f, top - pawnHeight);
        }

        /// <summary>
        /// True when a round gets through the shield: only sharp penetration counts (blunt-only rounds never penetrate).
        /// </summary>
        public static bool PenetratesShield(bool sharp, float penetration, float shieldArmor)
        {
            return sharp && RemainingPenetration(penetration, shieldArmor) > 0f;
        }

        /// <summary>Penetration left after the shield; &gt; 0 means the round got through.</summary>
        public static float RemainingPenetration(float penetration, float shieldArmor)
        {
            return penetration - shieldArmor;
        }
    }
}
