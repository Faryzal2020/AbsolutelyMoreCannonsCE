using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Test component that draws a bright yellow line from the turret barrel for a set number of ticks after firing.
    /// </summary>
    public class CompTracerLine : ThingComp
    {
        private CompProperties_TracerLine Props => (CompProperties_TracerLine)props;

        private int ticksRemaining = 0;
        private Material lineMat;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            lineMat = SolidColorMaterials.SimpleSolidColorMaterial(Props.lineColor);
        }

        /// <summary>
        /// Called when the turret fires a shot.
        /// </summary>
        public void OnFired()
        {
            ticksRemaining = Props.durationTicks;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (ticksRemaining > 0)
            {
                ticksRemaining--;
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();

            if (ticksRemaining <= 0)
                return;

            if (lineMat == null)
            {
                lineMat = SolidColorMaterials.SimpleSolidColorMaterial(Props.lineColor);
            }

            float turretRotation = GetTurretRotation();

            // Calculate forward direction vector (0 deg = North/Z+, 90 deg = East/X+)
            float angleRad = turretRotation * Mathf.Deg2Rad;
            Vector3 forwardDir = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)).normalized;

            // Rotate local offset vector by turret rotation
            Vector3 rotatedOffset = RotateVector(Props.lineOffset, turretRotation);

            // Barrel tip starting position (elevate slightly to render above turret top)
            Vector3 startPos = parent.DrawPos + rotatedOffset;
            startPos.y = AltitudeLayer.MetaOverlays.AltitudeFor();

            // End position extended by lineLength
            Vector3 endPos = startPos + forwardDir * Props.lineLength;

            // Draw line using native RimWorld GenDraw
            GenDraw.DrawLineBetween(startPos, endPos, lineMat, Props.lineWidth);
        }

        /// <summary>
        /// Gets current turret rotation angle.
        /// </summary>
        private float GetTurretRotation()
        {
            var barrelComp = parent.TryGetComp<CompTurretBarrel>();
            if (barrelComp != null)
            {
                float rot = barrelComp.GetCurrentBarrelRotation();
                if (!float.IsNaN(rot)) return rot;
            }

            if (parent is Building_Turret buildingTurret)
            {
                try
                {
                    var topField = buildingTurret.GetType().GetField("top",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (topField != null)
                    {
                        object top = topField.GetValue(buildingTurret);
                        if (top != null)
                        {
                            var curRotationProp = top.GetType().GetProperty("CurRotation",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            if (curRotationProp != null)
                            {
                                return (float)curRotationProp.GetValue(top);
                            }
                        }
                    }
                }
                catch { }
            }

            return parent.Rotation.AsAngle;
        }

        /// <summary>
        /// Rotates a vector by the given angle (in degrees) around Y axis
        /// </summary>
        private Vector3 RotateVector(Vector3 vector, float rotationDegrees)
        {
            float angleRad = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            return new Vector3(
                vector.x * cos + vector.z * sin,
                vector.y,
                -vector.x * sin + vector.z * cos
            );
        }
    }
}
