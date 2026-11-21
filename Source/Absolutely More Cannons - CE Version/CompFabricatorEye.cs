using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Renders the layered AI eye (eyeball, cover, and glow) on the Ammo Fabricator
    /// and animates the eyeball + glow to stare at nearby pawns in front of it or
    /// look around when idle.
    /// </summary>
    public class CompFabricatorEye : ThingComp
    {
        public CompProperties_FabricatorEye Props => (CompProperties_FabricatorEye)props;

        private CompPowerTrader powerComp;
        private CompFlickable flickComp;
        private CompBreakdownable breakdownComp;
        private CompAmmoCapacitor ammoCapacitorComp;

        private Material eyeballMat;
        private Material eyeballOffMat;
        private Material coverMat;
        private Material glowMat;
        private Material topOffMat;
        private Material topOnMat;
        private bool materialsInitialized = false;

        private Vector2 currentOffset = Vector2.zero;
        private Vector2 targetOffset = Vector2.zero;
        private Vector2 idleAnchorOffset = Vector2.zero;
        private Vector2 saccadeOffset = Vector2.zero;
        private Pawn trackedPawn = null;
        private int idleTicksRemaining = 0;
        private int saccadeTicksRemaining = 0;
        private int ignoreCheckTicksRemaining = 0;
        private int forcedIdleTicksRemaining = 0;
        private bool wasTrackingPawn = false;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
            breakdownComp = parent.GetComp<CompBreakdownable>();
            ammoCapacitorComp = parent.GetComp<CompAmmoCapacitor>();
        }

        public bool IsEyeActive => IsActive;
        public Pawn TrackedPawn => trackedPawn;
        public Vector3 EyeWorldPos => GetEyeNeutralWorldPos();

        private bool IsActive
        {
            get
            {
                if (!parent.Spawned) return false;
                if (flickComp != null && !flickComp.SwitchIsOn) return false;
                if (breakdownComp != null && breakdownComp.BrokenDown) return false;
                if (Props.requirePower && powerComp != null && !powerComp.PowerOn) return false;
                return true;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!parent.Spawned) return;

            if (!IsActive)
            {
                trackedPawn = null;
                targetOffset = Vector2.zero;
                saccadeOffset = Vector2.zero;
                forcedIdleTicksRemaining = 0;
                currentOffset = Vector2.Lerp(currentOffset, targetOffset, Props.idleSpeed);
                return;
            }

            if (forcedIdleTicksRemaining > 0)
            {
                forcedIdleTicksRemaining--;
                trackedPawn = null;
            }
            else
            {
                if (ignoreCheckTicksRemaining > 0)
                {
                    ignoreCheckTicksRemaining--;
                }
                else
                {
                    int minChk = Mathf.Max(20, Props.ignorePawnCheckIntervalMin);
                    int maxChk = Mathf.Max(minChk, Props.ignorePawnCheckIntervalMax);
                    ignoreCheckTicksRemaining = Rand.RangeInclusive(minChk, maxChk);

                    if (Rand.Chance(Props.ignorePawnChance))
                    {
                        int minDur = Mathf.Max(30, Props.ignorePawnDurationMin);
                        int maxDur = Mathf.Max(minDur, Props.ignorePawnDurationMax);
                        forcedIdleTicksRemaining = Rand.RangeInclusive(minDur, maxDur);
                        trackedPawn = null;
                        // Force picking a fresh random idle gaze anchor immediately
                        idleTicksRemaining = 0;
                        saccadeTicksRemaining = 0;
                    }
                }

                if (forcedIdleTicksRemaining <= 0 && parent.IsHashIntervalTick(10))
                {
                    trackedPawn = FindTargetPawn();
                }
            }

            bool hasValidPawn = trackedPawn != null && trackedPawn.Spawned && !trackedPawn.Dead && trackedPawn.Map == parent.Map;
            if (hasValidPawn != wasTrackingPawn)
            {
                wasTrackingPawn = hasValidPawn;
                saccadeOffset = Vector2.zero;
                saccadeTicksRemaining = 0;
                if (!hasValidPawn)
                {
                    idleTicksRemaining = 0;
                }
            }

            if (hasValidPawn)
            {
                UpdateTrackingTarget(trackedPawn);
                currentOffset = Vector2.Lerp(currentOffset, targetOffset, Props.trackSpeed);
            }
            else
            {
                trackedPawn = null;
                UpdateIdleTarget();
                currentOffset = Vector2.Lerp(currentOffset, targetOffset, Props.idleSpeed);
            }
        }

        private Vector3 GetGraphicBasePos()
        {
            Vector3 basePos = parent.DrawPos;
            if (parent.Graphic != null)
            {
                basePos += parent.Graphic.DrawOffset(parent.Rotation);
            }
            return basePos;
        }

        private Vector3 GetEyeNeutralWorldPos()
        {
            Vector3 basePos = GetGraphicBasePos();
            if (Props.eyeball != null)
            {
                basePos += Props.eyeball.offset;
            }
            return basePos;
        }

        private Pawn FindTargetPawn()
        {
            Map map = parent.Map;
            if (map == null) return null;

            Vector3 eyePos = GetEyeNeutralWorldPos();
            float maxDistSq = Props.detectRadius * Props.detectRadius;

            Pawn bestPawn = null;
            float bestScore = float.MaxValue;

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || !p.Spawned || p.Dead) continue;

                Vector3 pawnPos = p.DrawPos;
                float dz = pawnPos.z - eyePos.z;
                if (Props.onlyTrackInFront && dz > Props.frontZMargin) continue;

                float dx = pawnPos.x - eyePos.x;
                float distSq = dx * dx + dz * dz;
                if (distSq > maxDistSq) continue;

                // Prioritize moving pawns, then closer pawns
                bool moving = p.pather != null && p.pather.MovingNow;
                float score = distSq * (moving ? 0.35f : 1.0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPawn = p;
                }
            }

            return bestPawn;
        }

        private void UpdateTrackingTarget(Pawn pawn)
        {
            Vector3 eyePos = GetEyeNeutralWorldPos();
            Vector3 delta = pawn.DrawPos - eyePos;
            Vector2 dir2D = new Vector2(delta.x, delta.z);

            float dist = dir2D.magnitude;
            Vector2 baseGaze = Vector2.zero;
            if (dist >= 0.001f)
            {
                Vector2 norm = dir2D / dist;
                // Smoothly ramp gaze magnitude when pawn is very close to the eye center
                float strength = Mathf.Clamp01(dist / 1.5f);

                float targetX = (norm.x >= 0f ? norm.x * Props.maxOffsetRight : norm.x * Props.maxOffsetLeft) * strength;
                float targetZ = (norm.y >= 0f ? norm.y * Props.maxOffsetUp : norm.y * Props.maxOffsetDown) * strength;
                baseGaze = new Vector2(targetX, targetZ);
            }

            if (saccadeTicksRemaining > 0)
            {
                saccadeTicksRemaining--;
            }
            else
            {
                saccadeOffset = PickSaccadeOffset(baseGaze, Props.trackSaccadeMagnitude, saccadeOffset);
                int minSac = Mathf.Max(5, Props.trackSaccadeIntervalMin);
                int maxSac = Mathf.Max(minSac, Props.trackSaccadeIntervalMax);
                saccadeTicksRemaining = Rand.RangeInclusive(minSac, maxSac);
            }

            targetOffset = ClampOffset(baseGaze + saccadeOffset);
        }

        private void UpdateIdleTarget()
        {
            if (idleTicksRemaining > 0)
            {
                idleTicksRemaining--;
            }
            else
            {
                idleAnchorOffset = PickDistinctIdleAnchor(idleAnchorOffset);

                int minHold = Mathf.Max(15, Props.idleHoldTicksMin);
                int maxHold = Mathf.Max(minHold, Props.idleHoldTicksMax);
                idleTicksRemaining = Rand.RangeInclusive(minHold, maxHold);
                saccadeOffset = Vector2.zero;
                int minSac = Mathf.Max(5, Props.idleSaccadeIntervalMin);
                int maxSac = Mathf.Max(minSac, Props.idleSaccadeIntervalMax);
                saccadeTicksRemaining = Rand.RangeInclusive(minSac, maxSac);
            }

            if (saccadeTicksRemaining > 0)
            {
                saccadeTicksRemaining--;
            }
            else
            {
                saccadeOffset = PickSaccadeOffset(idleAnchorOffset, Props.idleSaccadeMagnitude, saccadeOffset);
                int minSac = Mathf.Max(5, Props.idleSaccadeIntervalMin);
                int maxSac = Mathf.Max(minSac, Props.idleSaccadeIntervalMax);
                saccadeTicksRemaining = Rand.RangeInclusive(minSac, maxSac);
            }

            targetOffset = ClampOffset(idleAnchorOffset + saccadeOffset);
        }

        private Vector2 PickDistinctIdleAnchor(Vector2 previousAnchor)
        {
            Vector2 best = Vector2.zero;
            float bestDistSq = -1f;
            for (int i = 0; i < 3; i++)
            {
                Vector2 candidate = ClampOffset(new Vector2(
                    Rand.Range(-Props.maxOffsetLeft, Props.maxOffsetRight),
                    Rand.Range(-Props.maxOffsetDown, Props.maxOffsetUp)));
                float dSq = (candidate - previousAnchor).sqrMagnitude;
                if (dSq > bestDistSq)
                {
                    bestDistSq = dSq;
                    best = candidate;
                }
            }
            return best;
        }

        private Vector2 PickSaccadeOffset(Vector2 anchor, Vector2 magnitude, Vector2 previousSaccade)
        {
            float minDx = Mathf.Max(-magnitude.x, -Props.maxOffsetLeft - anchor.x);
            float maxDx = Mathf.Min(magnitude.x, Props.maxOffsetRight - anchor.x);
            if (minDx > maxDx) { minDx = -magnitude.x; maxDx = magnitude.x; }

            float minDy = Mathf.Max(-magnitude.y, -Props.maxOffsetDown - anchor.y);
            float maxDy = Mathf.Min(magnitude.y, Props.maxOffsetUp - anchor.y);
            if (minDy > maxDy) { minDy = -magnitude.y; maxDy = magnitude.y; }

            Vector2 best = Vector2.zero;
            float bestDistSq = -1f;
            for (int i = 0; i < 2; i++)
            {
                Vector2 candidate = new Vector2(
                    Rand.Range(minDx, maxDx),
                    Rand.Range(minDy, maxDy));
                float dSq = (candidate - previousSaccade).sqrMagnitude;
                if (dSq > bestDistSq)
                {
                    bestDistSq = dSq;
                    best = candidate;
                }
            }
            return best;
        }

        private Vector2 ClampOffset(Vector2 offset)
        {
            offset.x = Mathf.Clamp(offset.x, -Props.maxOffsetLeft, Props.maxOffsetRight);
            offset.y = Mathf.Clamp(offset.y, -Props.maxOffsetDown, Props.maxOffsetUp);
            return offset;
        }

        private void EnsureMaterials()
        {
            if (materialsInitialized) return;
            materialsInitialized = true;

            eyeballMat = LoadLayerMaterial(Props.eyeball);
            eyeballOffMat = LoadLayerMaterial(Props.eyeballOff);
            coverMat = LoadLayerMaterial(Props.cover);
            glowMat = LoadLayerMaterial(Props.glow);
            topOffMat = LoadLayerMaterial(Props.topOff);
            topOnMat = LoadLayerMaterial(Props.topOn);
        }

        private static Material LoadLayerMaterial(FabricatorEyeLayerProperties layer)
        {
            if (layer == null || string.IsNullOrEmpty(layer.texPath)) return null;
            Texture2D tex = ContentFinder<Texture2D>.Get(layer.texPath, reportFailure: false);
            if (tex == null) return null;

            Shader shader = layer.useGlowShader ? ShaderDatabase.MoteGlow : ShaderDatabase.Transparent;
            return MaterialPool.MatFrom(tex, shader, layer.color);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned) return;

            EnsureMaterials();

            Vector3 graphicBasePos = GetGraphicBasePos();
            Vector3 basePos = graphicBasePos;
            basePos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            Vector2 mainSize = parent.Graphic != null ? parent.Graphic.drawSize : Vector2.one;
            Vector3 moveDelta = new Vector3(currentOffset.x, 0f, currentOffset.y);

            bool active = IsActive;

            // 1. Eyeball (uses OFF texture when unpowered/turned off/broken down)
            if (active || eyeballOffMat == null)
            {
                DrawLayer(Props.eyeball, eyeballMat, basePos, mainSize, moveDelta, 0.005f);
            }
            else
            {
                DrawLayer(Props.eyeballOff, eyeballOffMat, basePos, mainSize, moveDelta, 0.005f);
            }

            // 2. Cover (static on top of eyeball)
            DrawLayer(Props.cover, coverMat, basePos, mainSize, Vector3.zero, 0.015f);

            // 3. Glow (moves together with eyeball, on top of cover; hidden when unpowered/turned off)
            if (active)
            {
                DrawLayer(Props.glow, glowMat, basePos, mainSize, moveDelta, 0.025f);
            }

            // 4. Top overlay (above all pawn layers including head/hair/hats, below effects layer; driven by crafting state)
            Vector3 topBasePos = graphicBasePos;
            topBasePos.y = AltitudeLayer.MoteOverheadLow.AltitudeFor() - 0.005f;
            bool crafting = ammoCapacitorComp != null && ammoCapacitorComp.IsCraftingNow;
            if (crafting && topOnMat != null)
            {
                DrawLayer(Props.topOn, topOnMat, topBasePos, mainSize, Vector3.zero, 0f);
            }
            else
            {
                DrawLayer(Props.topOff, topOffMat, topBasePos, mainSize, Vector3.zero, 0f);
            }
        }

        private static void DrawLayer(
            FabricatorEyeLayerProperties layer,
            Material mat,
            Vector3 basePos,
            Vector2 mainSize,
            Vector3 dynamicOffset,
            float altitudeEpsilon)
        {
            if (layer == null || mat == null) return;

            Vector3 pos = basePos + layer.offset + dynamicOffset;
            pos.y = basePos.y + layer.offset.y + altitudeEpsilon;

            Vector3 scale = new Vector3(
                mainSize.x * layer.scale.x,
                1f,
                mainSize.y * layer.scale.y);

            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, scale);
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
        }
    }
}
