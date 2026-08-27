using System;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component properties for temporary test line drawing on turret firing.
    /// </summary>
    public class CompProperties_TracerLine : CompProperties
    {
        public float lineLength = 20f;            // Length of the line in tiles
        public float lineWidth = 0.06f;           // Thickness of line in tiles (~3 screen px)
        public Vector3 lineOffset = Vector3.zero;    // Barrel tip offset (X, Y, Z)
        public int durationTicks = 2;             // Ticks to remain visible after firing
        public Color lineColor = Color.yellow;    // Line color

        public CompProperties_TracerLine()
        {
            this.compClass = typeof(CompTracerLine);
        }
    }
}
