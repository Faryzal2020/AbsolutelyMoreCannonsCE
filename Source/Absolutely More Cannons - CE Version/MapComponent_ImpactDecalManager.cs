using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Data record for a single temporary ground impact decal (Crater + Soot overlay).
    /// Saved with the map and rendered dynamically without counting toward RimWorld's MoteCounter limit.
    /// </summary>
    public class ImpactGroundDecal : IExposable
    {
        public Vector3 position;
        public bool hasCrater;
        public bool hasSoot;

        public float craterScale = 3f;
        public float sootScale = 3f;

        public float craterRotation = 0f;
        public float sootRotation = 0f;

        public int craterTexIndex = 0;
        public int sootTexIndex = 0;

        public float craterBaseAlpha = 1.0f;
        public float sootBaseAlpha = 0.5f;

        public float craterAgeTicks = 0f;
        public float sootAgeTicks = 0f;

        public int craterSolidTicks = 80000;
        public int craterFadeTicks = 1800;

        public int sootSolidTicks = 40000;
        public int sootFadeTicks = 900;

        public float rainSootFadeMultiplier = 2.0f;

        public bool IsExpired
        {
            get
            {
                bool craterDone = !hasCrater || craterAgeTicks >= (craterSolidTicks + craterFadeTicks);
                bool sootDone = !hasSoot || sootAgeTicks >= (sootSolidTicks + sootFadeTicks);
                return craterDone && sootDone;
            }
        }

        public float GetCraterAlpha()
        {
            if (!hasCrater) return 0f;
            int total = craterSolidTicks + craterFadeTicks;
            if (craterAgeTicks >= total) return 0f;
            if (craterAgeTicks <= craterSolidTicks || craterFadeTicks <= 0) return craterBaseAlpha;
            float t = (craterAgeTicks - craterSolidTicks) / (float)craterFadeTicks;
            return craterBaseAlpha * Mathf.Clamp01(1f - t);
        }

        public float GetSootAlpha()
        {
            if (!hasSoot) return 0f;
            int total = sootSolidTicks + sootFadeTicks;
            if (sootAgeTicks >= total) return 0f;
            if (sootAgeTicks <= sootSolidTicks || sootFadeTicks <= 0) return sootBaseAlpha;
            float t = (sootAgeTicks - sootSolidTicks) / (float)sootFadeTicks;
            return sootBaseAlpha * Mathf.Clamp01(1f - t);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref position, "position", Vector3.zero);
            Scribe_Values.Look(ref hasCrater, "hasCrater", false);
            Scribe_Values.Look(ref hasSoot, "hasSoot", true);
            Scribe_Values.Look(ref craterScale, "craterScale", 3f);
            Scribe_Values.Look(ref sootScale, "sootScale", 3f);
            Scribe_Values.Look(ref craterRotation, "craterRotation", 0f);
            Scribe_Values.Look(ref sootRotation, "sootRotation", 0f);
            Scribe_Values.Look(ref craterTexIndex, "craterTexIndex", 0);
            Scribe_Values.Look(ref sootTexIndex, "sootTexIndex", 0);
            Scribe_Values.Look(ref craterBaseAlpha, "craterBaseAlpha", 1.0f);
            Scribe_Values.Look(ref sootBaseAlpha, "sootBaseAlpha", 0.5f);
            Scribe_Values.Look(ref craterAgeTicks, "craterAgeTicks", 0f);
            Scribe_Values.Look(ref sootAgeTicks, "sootAgeTicks", 0f);
            Scribe_Values.Look(ref craterSolidTicks, "craterSolidTicks", 80000);
            Scribe_Values.Look(ref craterFadeTicks, "craterFadeTicks", 1800);
            Scribe_Values.Look(ref sootSolidTicks, "sootSolidTicks", 40000);
            Scribe_Values.Look(ref sootFadeTicks, "sootFadeTicks", 900);
            Scribe_Values.Look(ref rainSootFadeMultiplier, "rainSootFadeMultiplier", 2.0f);
        }
    }

    /// <summary>
    /// MapComponent that manages and renders temporary shell impact craters and 50% opacity soot overlays.
    /// Avoids MoteCounter saturation while supporting independent random rotations, per-shell dynamic scaling,
    /// smooth alpha fade-out, and rain-accelerated soot dissipation.
    /// </summary>
    public class MapComponent_ImpactDecalManager : MapComponent
    {
        private List<ImpactGroundDecal> activeDecals = new List<ImpactGroundDecal>();

        private const int MaxActiveDecals = 1500;
        private const string CraterFolderPath = "Things/Building/Crater/3x3";
        private const string SootFolderPath = "Effects/Soot";

        private static List<Material> craterMaterials;
        private static List<Material> sootMaterials;
        private static MaterialPropertyBlock propertyBlock;
        private static bool materialsInitialized = false;

        public MapComponent_ImpactDecalManager(Map map) : base(map)
        {
        }

        private static void EnsureMaterialsInitialized()
        {
            if (materialsInitialized) return;
            materialsInitialized = true;

            craterMaterials = new List<Material>();
            sootMaterials = new List<Material>();
            propertyBlock = new MaterialPropertyBlock();

            Shader shader = ShaderDatabase.Transparent;

            try
            {
                IEnumerable<Texture2D> craterTextures = ContentFinder<Texture2D>.GetAllInFolder(CraterFolderPath);
                if (craterTextures != null)
                {
                    foreach (Texture2D tex in craterTextures)
                    {
                        if (tex != null && !tex.name.EndsWith("_m"))
                        {
                            Material mat = MaterialPool.MatFrom(tex, shader, Color.white);
                            if (mat != null)
                            {
                                craterMaterials.Add(mat);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC] Failed loading crater textures from '{CraterFolderPath}': {ex.Message}");
            }

            try
            {
                IEnumerable<Texture2D> sootTextures = ContentFinder<Texture2D>.GetAllInFolder(SootFolderPath);
                if (sootTextures != null)
                {
                    foreach (Texture2D tex in sootTextures)
                    {
                        if (tex != null && !tex.name.EndsWith("_m"))
                        {
                            Material mat = MaterialPool.MatFrom(tex, shader, Color.white);
                            if (mat != null)
                            {
                                sootMaterials.Add(mat);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC] Failed loading soot textures from '{SootFolderPath}': {ex.Message}");
            }
        }

        public void AddImpactDecal(
            Vector3 impactPos,
            bool spawnCrater,
            bool spawnSoot,
            float craterScale,
            float sootScale,
            ImpactGroundDecalExtension ext)
        {
            if (!spawnCrater && !spawnSoot) return;
            if (ext == null) return;

            EnsureMaterialsInitialized();

            if (activeDecals == null)
            {
                activeDecals = new List<ImpactGroundDecal>();
            }

            // Cap total decals on a single map so prolonged sieges stay performant
            if (activeDecals.Count >= MaxActiveDecals)
            {
                activeDecals.RemoveAt(0);
            }

            int craterCount = craterMaterials != null && craterMaterials.Count > 0 ? craterMaterials.Count : 1;
            int sootCount = sootMaterials != null && sootMaterials.Count > 0 ? sootMaterials.Count : 1;

            ImpactGroundDecal decal = new ImpactGroundDecal
            {
                position = new Vector3(impactPos.x, 0f, impactPos.z),
                hasCrater = spawnCrater && craterMaterials != null && craterMaterials.Count > 0,
                hasSoot = spawnSoot && sootMaterials != null && sootMaterials.Count > 0,
                craterScale = craterScale,
                sootScale = sootScale,
                craterRotation = Rand.Range(0f, 360f),
                sootRotation = Rand.Range(0f, 360f),
                craterTexIndex = Rand.Range(0, craterCount),
                sootTexIndex = Rand.Range(0, sootCount),
                craterBaseAlpha = ext.craterOpacity,
                sootBaseAlpha = ext.sootOpacity,
                craterAgeTicks = 0f,
                sootAgeTicks = 0f,
                craterSolidTicks = ext.craterSolidTicks,
                craterFadeTicks = ext.craterFadeTicks,
                sootSolidTicks = ext.sootSolidTicks,
                sootFadeTicks = ext.sootFadeTicks,
                rainSootFadeMultiplier = ext.rainSootFadeMultiplier
            };

            activeDecals.Add(decal);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (activeDecals == null || activeDecals.Count == 0) return;

            // Advance ages every 10 ticks for low overhead
            if (Find.TickManager.TicksGame % 10 != 0) return;

            const float stepTicks = 10f;
            float rainRate = map?.weatherManager?.RainRate ?? 0f;
            RoofGrid roofGrid = map?.roofGrid;

            for (int i = activeDecals.Count - 1; i >= 0; i--)
            {
                ImpactGroundDecal decal = activeDecals[i];
                if (decal == null)
                {
                    activeDecals.RemoveAt(i);
                    continue;
                }

                if (decal.hasCrater)
                {
                    decal.craterAgeTicks += stepTicks;
                }

                if (decal.hasSoot)
                {
                    float sootStep = stepTicks;
                    if (rainRate > 0.01f)
                    {
                        IntVec3 cell = decal.position.ToIntVec3();
                        bool isRoofed = roofGrid != null && cell.InBounds(map) && roofGrid.Roofed(cell);
                        if (!isRoofed)
                        {
                            sootStep *= (1f + rainRate * decal.rainSootFadeMultiplier);
                        }
                    }
                    decal.sootAgeTicks += sootStep;
                }

                if (decal.IsExpired)
                {
                    activeDecals.RemoveAt(i);
                }
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (activeDecals == null || activeDecals.Count == 0) return;
            if (Find.CurrentMap != map || Find.CameraDriver == null) return;

            EnsureMaterialsInitialized();

            bool hasCraterMats = craterMaterials != null && craterMaterials.Count > 0;
            bool hasSootMats = sootMaterials != null && sootMaterials.Count > 0;
            if (!hasCraterMats && !hasSootMats) return;

            CellRect viewRect = Find.CameraDriver.CurrentViewRect;
            FogGrid fogGrid = map.fogGrid;

            float baseCraterAlt = Altitudes.AltitudeFor(AltitudeLayer.FloorEmplacement);
            float baseSootAlt = Altitudes.AltitudeFor(AltitudeLayer.Filth);
            Mesh planeMesh = MeshPool.plane10;
            int colorPropId = ShaderPropertyIDs.Color;

            for (int i = 0; i < activeDecals.Count; i++)
            {
                ImpactGroundDecal decal = activeDecals[i];
                if (decal == null) continue;

                float maxRadius = Mathf.Max(decal.craterScale, decal.sootScale) * 0.55f + 1f;
                int expand = Mathf.CeilToInt(maxRadius);
                IntVec3 cell = decal.position.ToIntVec3();

                if (!viewRect.ExpandedBy(expand).Contains(cell))
                {
                    continue;
                }

                if (fogGrid != null && cell.InBounds(map) && fogGrid.IsFogged(cell))
                {
                    continue;
                }

                // Micro Y offset per index prevents Z-fighting when multiple shells land near each other
                float yMicroOffset = (i % 64) * 0.0001f;

                // 1. Draw Crater Layer (bottom)
                if (decal.hasCrater && hasCraterMats)
                {
                    float craterAlpha = decal.GetCraterAlpha();
                    if (craterAlpha > 0.005f)
                    {
                        Material mat = craterMaterials[Mathf.Abs(decal.craterTexIndex) % craterMaterials.Count];
                        Vector3 drawPos = new Vector3(decal.position.x, baseCraterAlt + yMicroOffset, decal.position.z);
                        Quaternion rot = Quaternion.AngleAxis(decal.craterRotation, Vector3.up);
                        Vector3 scale = new Vector3(decal.craterScale, 1f, decal.craterScale);
                        Matrix4x4 matrix = Matrix4x4.TRS(drawPos, rot, scale);

                        propertyBlock.SetColor(colorPropId, new Color(1f, 1f, 1f, craterAlpha));
                        Graphics.DrawMesh(planeMesh, matrix, mat, 0, null, 0, propertyBlock);
                    }
                }

                // 2. Draw Soot Layer (overlayed on top of crater at 50% opacity)
                if (decal.hasSoot && hasSootMats)
                {
                    float sootAlpha = decal.GetSootAlpha();
                    if (sootAlpha > 0.005f)
                    {
                        Material mat = sootMaterials[Mathf.Abs(decal.sootTexIndex) % sootMaterials.Count];
                        Vector3 drawPos = new Vector3(decal.position.x, baseSootAlt + yMicroOffset, decal.position.z);
                        Quaternion rot = Quaternion.AngleAxis(decal.sootRotation, Vector3.up);
                        Vector3 scale = new Vector3(decal.sootScale, 1f, decal.sootScale);
                        Matrix4x4 matrix = Matrix4x4.TRS(drawPos, rot, scale);

                        propertyBlock.SetColor(colorPropId, new Color(1f, 1f, 1f, sootAlpha));
                        Graphics.DrawMesh(planeMesh, matrix, mat, 0, null, 0, propertyBlock);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeDecals, "activeDecals", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && activeDecals == null)
            {
                activeDecals = new List<ImpactGroundDecal>();
            }
        }
    }
}
