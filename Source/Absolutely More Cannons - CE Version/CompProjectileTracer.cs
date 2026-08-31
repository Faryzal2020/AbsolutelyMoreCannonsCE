using System;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using CombatExtended;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component attached to a projectile (vanilla or Combat Extended) that renders a tracer line extending behind it in 2D screen space.
    /// </summary>
    public class CompProjectileTracer : ThingComp
    {
        private CompProperties_ProjectileTracer Props => (CompProperties_ProjectileTracer)props;

        private int ticksAlive = 0;
        private Vector3 lastDrawPos = Vector3.zero;
        private Vector3 lastFlightDirection = Vector3.forward;
        private Material lineMat;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ticksAlive = 0;
            lineMat = SolidColorMaterials.SimpleSolidColorMaterial(Props.GetEffectiveColor());
            lastDrawPos = parent.DrawPos;
        }

        public override void CompTick()
        {
            base.CompTick();
            ticksAlive++;
        }

        public override void PostDraw()
        {
            base.PostDraw();

            // Do not draw until delay ticks have elapsed
            if (ticksAlive < Props.GetDelayTicks())
                return;

            if (lineMat == null)
            {
                lineMat = SolidColorMaterials.SimpleSolidColorMaterial(Props.GetEffectiveColor());
            }

            // Use parent.DrawPos directly - this is the 2D visual position of the shell in the air (NOT the ground shadow!)
            Vector3 currentPos = parent.DrawPos;
            Vector3 forwardDir = GetProjectileDirection(currentPos);

            if (forwardDir != Vector3.zero)
            {
                lastFlightDirection = forwardDir;
            }
            else
            {
                forwardDir = lastFlightDirection;
            }

            // Start of line is at the shell's airborne visual position
            Vector3 startPos = currentPos;
            startPos.y += 0.01f; // Render slightly above shell graphic layer

            // End of line extends BEHIND the shell (180 degrees opposite of 2D screen motion vector)
            float lineLen = Props.GetLineLength();
            Vector3 endPos = startPos - (forwardDir * lineLen);
            endPos.y = startPos.y;

            // Draw line from airborne shell position backward in screen space
            GenDraw.DrawLineBetween(startPos, endPos, lineMat, Props.lineWidth);

            AMCLogger.LogProjectileTracer($"Proj #{parent.ThingID} ({parent.def.defName}) | Tick:{ticksAlive} | DrawPos:{currentPos} | Start:{startPos} -> End:{endPos} | Dir:{forwardDir} | Len:{lineLen:F1}");

            lastDrawPos = currentPos;
        }

        /// <summary>
        /// Gets normalized 2D screen flight direction vector for the projectile.
        /// Accounts for the 2D screen vertical shift factor (y * 0.84) caused by vertical velocity (vy).
        /// </summary>
        private Vector3 GetProjectileDirection(Vector3 currentPos)
        {
            if (parent == null) return lastFlightDirection;

            // 1. Primary for CombatExtended Projectiles: Direct velocity vector transformed to 2D screen space
            // dz_screen = vz + (vy * 0.84f) due to RimWorld 2D orthographic elevation factor
            if (parent is ProjectileCE projCE)
            {
                Vector3 vel = projCE.velocity;
                if (vel.sqrMagnitude > 0.0001f)
                {
                    float dx = vel.x;
                    float dz = vel.z + (vel.y * 0.84f);
                    Vector3 screenVel = new Vector3(dx, 0f, dz);
                    if (screenVel.sqrMagnitude > 0.0001f)
                    {
                        return screenVel.normalized;
                    }
                }
            }

            // 2. Secondary: 2D Screen displacement vector between consecutive rendered frames (DrawPos delta)
            if (lastDrawPos != Vector3.zero && currentPos != lastDrawPos && ticksAlive > Props.GetDelayTicks() + 1)
            {
                Vector3 delta = currentPos - lastDrawPos;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    return delta.normalized;
                }
            }

            Type type = parent.GetType();

            // 3. Fallback for non-CE projectiles: Reflection velocity
            FieldInfo velField = type.GetField("velocity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (velField != null)
            {
                object val = velField.GetValue(parent);
                if (val is Vector3 vel && (vel.x != 0f || vel.y != 0f || vel.z != 0f))
                {
                    float dx = vel.x;
                    float dz = vel.z + (vel.y * 0.84f);
                    Vector3 screenVel = new Vector3(dx, 0f, dz);
                    if (screenVel.sqrMagnitude > 0.0001f)
                    {
                        return screenVel.normalized;
                    }
                }
            }

            // 3. Fallback: DrawRotation / ExactRotation (3D horizontal angle)
            PropertyInfo drawRotProp = type.GetProperty("DrawRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? type.GetProperty("ExactRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (drawRotProp != null)
            {
                object val = drawRotProp.GetValue(parent);
                if (val is Quaternion rot)
                {
                    float angleDeg = rot.eulerAngles.y;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    Vector3 rotDir = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)).normalized;
                    if (rotDir.sqrMagnitude > 0.0001f) return rotDir;
                }
            }

            // 4. Fallback: Vanilla exactRotation (float angle in degrees)
            FieldInfo rotField = type.GetField("exactRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (rotField != null)
            {
                object val = rotField.GetValue(parent);
                if (val is float angleDeg)
                {
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)).normalized;
                }
            }

            return lastFlightDirection;
        }
    }
}
