using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Manages and renders floating speech/thought bubbles for Ammo Fabricators on a map.
    /// Matches Jaxe's Interaction Bubbles UX (tick-based fade, hover transparency, click-through,
    /// zoom scaling) and softly respects Interaction Bubbles' global toggle when installed.
    /// </summary>
    public class MapComponent_FabricatorBubbles : MapComponent
    {
        private class ActiveBubble
        {
            public Thing anchorThing;
            public Vector3 worldPos;
            public string text;
            public int createdTick;
            public int durationTicks;
        }

        private readonly List<ActiveBubble> activeBubbles = new List<ActiveBubble>();

        private const int FadeTicks = 20;
        private const float MaxBubbleWidth = 230f;
        private const float PaddingX = 10f;
        private const float PaddingY = 6f;

        private static readonly Color BackgroundColor = new Color(0.08f, 0.11f, 0.14f, 0.90f);
        private static readonly Color BorderColor = new Color(0.32f, 0.64f, 0.74f, 0.95f);
        private static readonly Color TextColor = new Color(0.92f, 0.97f, 1.0f, 1.0f);

        private static bool ibReflectionChecked = false;
        private static FieldInfo ibActivatedField = null;
        private static PropertyInfo ibActivatedProp = null;

        public MapComponent_FabricatorBubbles(Map map) : base(map)
        {
        }

        public void AddBubble(Thing anchorThing, Vector3 worldPos, string text, int durationTicks)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Replace any existing bubble on the same building so bubbles never stack messily
            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                if (activeBubbles[i].anchorThing == anchorThing)
                {
                    activeBubbles.RemoveAt(i);
                }
            }

            activeBubbles.Add(new ActiveBubble
            {
                anchorThing = anchorThing,
                worldPos = worldPos,
                text = text,
                createdTick = Find.TickManager.TicksGame,
                durationTicks = Mathf.Max(60, durationTicks)
            });
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (activeBubbles.Count == 0) return;

            int now = Find.TickManager.TicksGame;
            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                ActiveBubble b = activeBubbles[i];
                if (b.anchorThing == null || !b.anchorThing.Spawned || b.anchorThing.Destroyed ||
                    now - b.createdTick >= b.durationTicks)
                {
                    activeBubbles.RemoveAt(i);
                }
            }
        }

        public void DrawBubblesOnGUI()
        {
            if (activeBubbles.Count == 0) return;
            if (!(TurretBarrelAnimationMod.settings?.enableFabricatorBubbles ?? true)) return;
            if (!ShouldShowWithInteractionBubbles()) return;
            if (Find.Camera == null || Find.CameraDriver == null) return;

            // Hide at furthest zoom to avoid clutter, matching Interaction Bubbles behavior
            if (Find.CameraDriver.CurrentZoom == CameraZoomRange.Furthest) return;

            int now = Find.TickManager.TicksGame;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            for (int i = 0; i < activeBubbles.Count; i++)
            {
                ActiveBubble b = activeBubbles[i];
                if (b.anchorThing == null || !b.anchorThing.Spawned) continue;

                int age = now - b.createdTick;
                if (age < 0 || age >= b.durationTicks) continue;

                float alpha = ComputeAlpha(age, b.durationTicks);
                if (alpha <= 0.01f) continue;

                Vector3 screenPos = Find.Camera.WorldToScreenPoint(b.worldPos);
                if (screenPos.z <= 0f) continue;

                // Convert Unity screen coords (bottom-left origin) to IMGUI coords (top-left origin)
                float guiX = screenPos.x / Prefs.UIScale;
                float guiY = (Screen.height - screenPos.y) / Prefs.UIScale;

                Vector2 textSize = Text.CalcSize(b.text);
                float width = Mathf.Min(MaxBubbleWidth, textSize.x + PaddingX * 2f);
                float height = Text.CalcHeight(b.text, width - PaddingX * 2f) + PaddingY * 2f;

                Rect rect = new Rect(guiX - width * 0.5f, guiY - height, width, height);
                if (rect.xMax < 0f || rect.xMin > UI.screenWidth || rect.yMax < 0f || rect.yMin > UI.screenHeight)
                {
                    continue;
                }

                // Dim bubble on mouse hover so it never obscures clicks or inspection
                if (Mouse.IsOver(rect))
                {
                    alpha *= 0.25f;
                }

                Color bg = BackgroundColor;
                bg.a *= alpha;
                Color border = BorderColor;
                border.a *= alpha;
                Color txt = TextColor;
                txt.a *= alpha;

                Widgets.DrawBoxSolidWithOutline(rect, bg, border, 1);

                GUI.color = txt;
                Rect textRect = rect.ContractedBy(PaddingX * 0.5f, PaddingY * 0.5f);
                Widgets.Label(textRect, b.text);
            }

            GUI.color = prevColor;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        private static float ComputeAlpha(int age, int duration)
        {
            if (age < FadeTicks)
            {
                return Mathf.Clamp01((float)age / FadeTicks);
            }
            int remaining = duration - age;
            if (remaining < FadeTicks)
            {
                return Mathf.Clamp01((float)remaining / FadeTicks);
            }
            return 1f;
        }

        /// <summary>
        /// Soft-compatibility with Jaxe's Interaction Bubbles: if installed and sync is enabled,
        /// mirrors its global on/off toggle without requiring a hard assembly reference.
        /// </summary>
        private static bool ShouldShowWithInteractionBubbles()
        {
            if (!(TurretBarrelAnimationMod.settings?.syncWithInteractionBubbles ?? true))
            {
                return true;
            }

            if (!ibReflectionChecked)
            {
                ibReflectionChecked = true;
                try
                {
                    Type settingsType = AccessTools.TypeByName("Bubbles.Settings")
                                     ?? AccessTools.TypeByName("Bubbles.Core.Settings");
                    if (settingsType != null)
                    {
                        ibActivatedField = AccessTools.Field(settingsType, "Activated");
                        ibActivatedProp = AccessTools.Property(settingsType, "Activated");
                    }
                }
                catch
                {
                    // Ignore reflection errors if Interaction Bubbles internals differ
                }
            }

            try
            {
                if (ibActivatedField != null && ibActivatedField.IsStatic)
                {
                    object val = ibActivatedField.GetValue(null);
                    if (val is bool b) return b;
                }
                if (ibActivatedProp != null && ibActivatedProp.GetGetMethod(true)?.IsStatic == true)
                {
                    object val = ibActivatedProp.GetValue(null);
                    if (val is bool b) return b;
                }
            }
            catch
            {
                // Fail open
            }

            return true;
        }
    }
}
