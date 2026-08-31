using System;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component properties for attaching a tracer line behind a projectile shell.
    /// Supports configurable length, width, delay, color (Color tuple or Hex string), and opacity/alpha.
    /// </summary>
    public class CompProperties_ProjectileTracer : CompProperties
    {
        public int delay = 0;                  // Ticks after launch before line appears
        public int delayTicks = -1;            // Alias for XML compatibility
        public float length = 10f;             // Length of line in tiles extending behind the bullet
        public float lineLength = -1f;         // Alias for XML compatibility
        public float lineWidth = 0.06f;        // Thickness of line in tiles (~3px)
        public Color lineColor = Color.yellow; // Color of line (XML tuple: (r, g, b) or (r, g, b, a))
        public string color = null;            // Hex string color alias (e.g. "#FFD700" or "FFD700")
        public float opacity = 1.0f;           // Line opacity/alpha multiplier (0.0 to 1.0)
        public float alpha = -1f;              // Alias for opacity

        public int GetDelayTicks() => delayTicks >= 0 ? delayTicks : delay;
        public float GetLineLength() => lineLength >= 0f ? lineLength : length;

        public Color GetEffectiveColor()
        {
            Color baseColor = lineColor;
            if (!string.IsNullOrEmpty(color))
            {
                string hex = color.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (ColorUtility.TryParseHtmlString(hex, out Color parsedColor))
                {
                    baseColor = parsedColor;
                }
            }

            float effectiveOpacity = (alpha >= 0f) ? alpha : opacity;
            effectiveOpacity = Mathf.Clamp01(effectiveOpacity);
            baseColor.a *= effectiveOpacity;

            return baseColor;
        }

        public CompProperties_ProjectileTracer()
        {
            this.compClass = typeof(CompProjectileTracer);
        }
    }
}
