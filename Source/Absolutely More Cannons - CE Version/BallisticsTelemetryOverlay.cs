using System;
using System.Collections.Generic;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Snapshot of a single turret shot's ballistic geometry and deviation contributors.
    /// </summary>
    public class TurretShotTelemetry
    {
        public int TickFired;
        public string TurretLabel;
        public string ProjectileDefName;
        public string TrajectoryWorkerName;
        public int ShotIndexInBurst;
        public int BurstShotCount;

        // Source & target geometry
        public float ShotHeight;
        public float TargetHeight;
        public float HorizontalRange;
        public float ShotSpeed;
        public float GravityPerWidth;

        // Vectors relative to horizon / North (0=N, 90=E, 180=S, 270=W)
        public float DirectLosPitchDeg;       // Straight-line elevation to target: atan2(dH, range)
        public float IdealBallisticPitchDeg;  // 100% accuracy zeroed elevation
        public float IdealYawDeg;             // 100% accuracy yaw
        public float ActualPitchDeg;          // Final shell pitch relative to horizon
        public float ActualYawDeg;            // Final shell yaw

        // Total deviation vs 100% accuracy vector
        public float TotalDeltaYawDeg;
        public float TotalDeltaPitchDeg;
        public float TotalAngularErrorDeg;

        // Per-layer contributors (degrees)
        public float AimShiftYawDeg;          // Circular + Lead + Range-est lateral shift
        public float AimShiftPitchDeg;        // Range-est + Radial shift changing zeroed pitch
        public float SwayYawDeg;
        public float SwayPitchDeg;
        public float RecoilYawDeg;
        public float RecoilPitchDeg;
        public float SpreadYawDeg;
        public float SpreadPitchDeg;
        public float ClampYawDeg;
        public float ClampPitchDeg;

        // Underlying ShiftVecReport parameters
        public float ShotDist;
        public float EstimatedTargDist;
        public float DistShiftMax;
        public float VisibilityShift;
        public float CircularMissRadius;
        public float IndirectFireShift;
        public float LeadShift;
        public float SwayAmplitude;
        public float SpreadDegrees;
        public float AimingAccuracy;
        public float SightsEfficiency;
        public float ShootingAccuracy;
        public float LightingShift;
        public float WeatherShift;
        public float SmokeDensity;
        public string RecoilControlInfo;
    }

    /// <summary>
    /// Captures per-shot ballistic geometry and deviation layers for turrets without altering CE RNG.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_BallisticsTelemetry
    {
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> shotAngleRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("shotAngle");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> shotRotationRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("shotRotation");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> lastShotAngleRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("lastShotAngle");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> lastShotRotationRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("lastShotRotation");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> rotationDegreesRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("rotationDegrees");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> angleRadiansRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("angleRadians");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> estimatedTargDistRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("estimatedTargDist");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, int>("numShotsFired");

        private static readonly Func<Verb_LaunchProjectileCE, float> getShotSpeed =
            (Func<Verb_LaunchProjectileCE, float>)Delegate.CreateDelegate(
                typeof(Func<Verb_LaunchProjectileCE, float>),
                AccessTools.PropertyGetter(typeof(Verb_LaunchProjectileCE), "ShotSpeed"));

        private struct PendingShotState
        {
            public bool Active;
            public TurretShotTelemetry Telemetry;
            public float PreSwayRot;
            public float PreSwayAngRad;
            public float PreRecoilRot;
            public float PreRecoilAngRad;
            public float PostShiftYaw;
            public float PostShiftPitchDeg;
        }

        [ThreadStatic]
        private static PendingShotState pending;

        static HarmonyPatches_BallisticsTelemetry()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.BallisticsTelemetry");

            var shiftTargetMethod = AccessTools.Method(
                typeof(Verb_LaunchProjectileCE),
                nameof(Verb_LaunchProjectileCE.ShiftTarget),
                new[] { typeof(ShiftVecReport), typeof(Vector3), typeof(float).MakeByRefType(), typeof(bool), typeof(bool) });
            if (shiftTargetMethod != null)
            {
                harmony.Patch(
                    shiftTargetMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Prefix_ShiftTarget)),
                    postfix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Postfix_ShiftTarget)) { priority = Priority.First });
            }

            var swayMethod = AccessTools.Method(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.GetSwayVec));
            if (swayMethod != null)
            {
                harmony.Patch(
                    swayMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Prefix_GetSwayVec)) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Postfix_GetSwayVec)) { priority = Priority.Last });
            }

            var recoilMethod = AccessTools.Method(typeof(Verb_LaunchProjectileCE), "GetRecoilVec");
            if (recoilMethod != null)
            {
                harmony.Patch(
                    recoilMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Prefix_GetRecoilVec)) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Postfix_GetRecoilVec)) { priority = Priority.Last });
            }

            var launchMethod = AccessTools.Method(
                typeof(ProjectileCE),
                nameof(ProjectileCE.Launch),
                new[] { typeof(Thing), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Thing), typeof(float) });
            if (launchMethod != null)
            {
                harmony.Patch(
                    launchMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches_BallisticsTelemetry), nameof(Postfix_ProjectileLaunch)));
            }
        }

        private static bool ShouldTrackVerb(Verb_LaunchProjectileCE verb)
        {
            if (verb == null || verb.caster == null || !verb.caster.Spawned) return false;
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || !settings.showBallisticsOverlay) return false;
            return verb.caster is Building_Turret;
        }

        public static void Prefix_ShiftTarget(
            Verb_LaunchProjectileCE __instance,
            ShiftVecReport report,
            Vector3 v,
            bool calculateMechanicalOnly)
        {
            if (calculateMechanicalOnly || !ShouldTrackVerb(__instance))
            {
                pending.Active = false;
                return;
            }

            Vector3 u = __instance.caster.TrueCenter();
            float shotHeight = __instance.ShotHeight;
            float speed = getShotSpeed(__instance);
            var pprops = __instance.projectilePropsCE;

            Vector2 src2 = new Vector2(u.x, u.z);
            Vector2 dst2 = new Vector2(v.x, v.z);
            float horizRange = (dst2 - src2).magnitude;

            float idealYaw = 0f;
            if (pprops != null)
            {
                idealYaw = pprops.TrajectoryWorker.ShotRotation(pprops, u, v) % 360f;
                if (idealYaw < 0f) idealYaw += 360f;
            }

            var t = new TurretShotTelemetry
            {
                TickFired = Find.TickManager?.TicksGame ?? 0,
                TurretLabel = __instance.caster.LabelShortCap,
                ProjectileDefName = __instance.Projectile?.defName ?? "?",
                TrajectoryWorkerName = pprops?.TrajectoryWorker?.GetType().Name ?? "?",
                ShotIndexInBurst = numShotsFiredRef(__instance) + 1,
                BurstShotCount = __instance.ShotsPerBurst,
                ShotHeight = shotHeight,
                HorizontalRange = horizRange,
                ShotSpeed = speed,
                GravityPerWidth = pprops?.GravityPerWidth ?? 0f,
                IdealYawDeg = idealYaw,
                ShotDist = report?.shotDist ?? horizRange,
                DistShiftMax = report?.distShift ?? 0f,
                VisibilityShift = report?.visibilityShift ?? 0f,
                CircularMissRadius = report?.circularMissRadius ?? 0f,
                IndirectFireShift = report?.indirectFireShift ?? 0f,
                LeadShift = report?.leadShift ?? 0f,
                SwayAmplitude = report?.swayDegrees ?? 0f,
                SpreadDegrees = report?.spreadDegrees ?? 0f,
                AimingAccuracy = report?.aimingAccuracy ?? 1f,
                SightsEfficiency = report?.sightsEfficiency ?? 1f,
                ShootingAccuracy = __instance.ShootingAccuracy,
                LightingShift = report?.lightingShift ?? 0f,
                WeatherShift = report?.weatherShift ?? 0f,
                SmokeDensity = report?.smokeDensity ?? 0f,
                RecoilControlInfo = BuildRecoilControlInfo(__instance)
            };

            pending = new PendingShotState
            {
                Active = true,
                Telemetry = t
            };
        }

        private static string BuildRecoilControlInfo(Verb_LaunchProjectileCE verb)
        {
            if (!HarmonyPatches_TurretRecoil.IsMountedTurretVerb(verb, out Building_Turret turret))
            {
                return $"shot #{numShotsFiredRef(verb) + 1}";
            }

            Thing eq = verb.EquipmentSource;
            TurretRecoilExtension ext = HarmonyPatches_TurretRecoil.GetRecoilExtension(turret, eq);
            TurretControlMode mode = HarmonyPatches_TurretRecoil.ResolveControlMode(turret, eq, ext);
            float act = HarmonyPatches_TurretRecoil.ResolveActuatorStrength(turret, eq, ext, mode);
            string modeLabel = mode == TurretControlMode.MachineActuated ? "Terminal" : "Mechanical";
            return $"#{numShotsFiredRef(verb) + 1} | {modeLabel}, act={act:F2}";
        }

        public static void Prefix_GetSwayVec(Verb_LaunchProjectileCE __instance, float rotation, float angle)
        {
            if (!pending.Active) return;
            pending.PreSwayRot = rotation;
            pending.PreSwayAngRad = angle;
        }

        public static void Postfix_GetSwayVec(Verb_LaunchProjectileCE __instance, float rotation, float angle)
        {
            if (!pending.Active || pending.Telemetry == null) return;
            pending.Telemetry.SwayYawDeg = rotation - pending.PreSwayRot;
            pending.Telemetry.SwayPitchDeg = (angle - pending.PreSwayAngRad) * Mathf.Rad2Deg;
        }

        public static void Prefix_GetRecoilVec(Verb_LaunchProjectileCE __instance, float rotation, float angle)
        {
            if (!pending.Active) return;
            pending.PreRecoilRot = rotation;
            pending.PreRecoilAngRad = angle;
        }

        public static void Postfix_GetRecoilVec(Verb_LaunchProjectileCE __instance, float rotation, float angle)
        {
            if (!pending.Active || pending.Telemetry == null) return;
            pending.Telemetry.RecoilYawDeg = rotation - pending.PreRecoilRot;
            pending.Telemetry.RecoilPitchDeg = (angle - pending.PreRecoilAngRad) * Mathf.Rad2Deg;
        }

        public static void Postfix_ShiftTarget(
            Verb_LaunchProjectileCE __instance,
            ShiftVecReport report,
            Vector3 v,
            float targetHeight,
            bool calculateMechanicalOnly)
        {
            if (!pending.Active || calculateMechanicalOnly || pending.Telemetry == null) return;

            var t = pending.Telemetry;
            var pprops = __instance.projectilePropsCE;
            Vector3 u = __instance.caster.TrueCenter();

            t.TargetHeight = targetHeight;
            t.EstimatedTargDist = estimatedTargDistRef(__instance);

            float dH = targetHeight - t.ShotHeight;
            t.DirectLosPitchDeg = Mathf.Atan2(dH, Mathf.Max(0.001f, t.HorizontalRange)) * Mathf.Rad2Deg;

            float idealPitchRad = 0f;
            if (pprops != null)
            {
                idealPitchRad = pprops.TrajectoryWorker.ShotAngle(
                    pprops,
                    u.WithY(t.ShotHeight),
                    v.WithY(targetHeight),
                    t.ShotSpeed);
            }
            t.IdealBallisticPitchDeg = idealPitchRad * Mathf.Rad2Deg;

            float lastRot = lastShotRotationRef(__instance);
            float lastAngRad = lastShotAngleRef(__instance);
            float rotDeg = rotationDegreesRef(__instance);
            float angRad = angleRadiansRef(__instance);
            float finalRot = shotRotationRef(__instance);
            float finalAngRad = shotAngleRef(__instance);

            t.AimShiftYawDeg = Mathf.DeltaAngle(t.IdealYawDeg, lastRot);
            t.AimShiftPitchDeg = (lastAngRad - idealPitchRad) * Mathf.Rad2Deg;

            t.SpreadYawDeg = Mathf.DeltaAngle(lastRot + rotDeg, finalRot);
            t.SpreadPitchDeg = (finalAngRad - angRad) * Mathf.Rad2Deg;

            pending.PostShiftYaw = finalRot;
            pending.PostShiftPitchDeg = finalAngRad * Mathf.Rad2Deg;
        }

        public static void Postfix_ProjectileLaunch(
            ProjectileCE __instance,
            Thing launcher,
            float shotAngle,
            float shotRotation,
            float shotHeight,
            float shotSpeed)
        {
            if (!pending.Active || pending.Telemetry == null) return;

            var t = pending.Telemetry;
            float launchYaw = shotRotation % 360f;
            if (launchYaw < 0f) launchYaw += 360f;
            float launchPitchDeg = shotAngle * Mathf.Rad2Deg;

            t.ClampYawDeg = Mathf.DeltaAngle(pending.PostShiftYaw, launchYaw);
            t.ClampPitchDeg = launchPitchDeg - pending.PostShiftPitchDeg;

            t.ActualYawDeg = launchYaw;
            t.ActualPitchDeg = launchPitchDeg;
            t.ShotHeight = shotHeight;
            t.ShotSpeed = shotSpeed;

            t.TotalDeltaYawDeg = Mathf.DeltaAngle(t.IdealYawDeg, launchYaw);
            t.TotalDeltaPitchDeg = launchPitchDeg - t.IdealBallisticPitchDeg;
            t.TotalAngularErrorDeg = Mathf.Sqrt(t.TotalDeltaYawDeg * t.TotalDeltaYawDeg + t.TotalDeltaPitchDeg * t.TotalDeltaPitchDeg);

            var map = __instance.Map ?? launcher?.Map;
            map?.GetComponent<MapComponent_BallisticsOverlay>()?.RecordShot(t);

            pending.Active = false;
        }
    }

    /// <summary>
    /// Draws the live on-screen turret ballistics telemetry HUD.
    /// </summary>
    public class MapComponent_BallisticsOverlay : MapComponent
    {
        private const int MaxHistory = 10;
        private readonly List<TurretShotTelemetry> history = new List<TurretShotTelemetry>();
        private int selectedIndex = -1;
        private bool paused = false;
        private bool minimized = false;
        private Rect windowRect = new Rect(16f, 110f, 460f, 430f);

        public Rect CurrentHUDRect
        {
            get
            {
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null || !settings.showBallisticsOverlay || Find.CurrentMap != map)
                    return Rect.zero;
                if (minimized)
                    return new Rect(windowRect.x, windowRect.y, 170f, 26f);
                if (history.Count == 0)
                    return new Rect(windowRect.x, windowRect.y, 310f, 52f);
                return windowRect;
            }
        }

        public MapComponent_BallisticsOverlay(Map map) : base(map)
        {
        }

        public void RecordShot(TurretShotTelemetry telemetry)
        {
            if (paused || telemetry == null) return;
            history.Add(telemetry);
            if (history.Count > MaxHistory)
            {
                history.RemoveAt(0);
            }
            selectedIndex = history.Count - 1;
        }

        public override void MapComponentOnGUI()
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || !settings.showBallisticsOverlay) return;
            if (Find.CurrentMap != map) return;

            if (minimized)
            {
                Rect pillRect = new Rect(windowRect.x, windowRect.y, 170f, 26f);
                Widgets.DrawBoxSolidWithOutline(pillRect, new Color(0.08f, 0.09f, 0.11f, 0.88f), new Color(0.4f, 0.45f, 0.55f, 0.8f));
                if (Widgets.ButtonText(pillRect, $"Ballistics HUD ({history.Count})"))
                {
                    minimized = false;
                }
                return;
            }

            if (history.Count == 0)
            {
                Rect emptyRect = new Rect(windowRect.x, windowRect.y, 310f, 52f);
                Widgets.DrawBoxSolidWithOutline(emptyRect, new Color(0.08f, 0.09f, 0.11f, 0.85f), new Color(0.35f, 0.4f, 0.5f, 0.8f));
                Text.Font = GameFont.Tiny;
                Widgets.Label(emptyRect.ContractedBy(6f), "Ballistics Viewer: Waiting for a turret to fire...\n(Disable in Mod Settings -> Show Live Turret Ballistics Viewer)");
                Text.Font = GameFont.Small;
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, history.Count - 1);
            TurretShotTelemetry t = history[selectedIndex];

            Widgets.DrawBoxSolidWithOutline(windowRect, new Color(0.07f, 0.08f, 0.10f, 0.90f), new Color(0.42f, 0.48f, 0.58f, 0.9f));

            Rect inner = windowRect.ContractedBy(8f);
            float y = inner.y;

            // Header bar + controls
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.85f, 0.93f, 1f);
            Widgets.Label(new Rect(inner.x, y, 220f, 22f), $"Ballistics: {t.TurretLabel}");
            GUI.color = Color.white;

            float bx = inner.xMax - 190f;
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(new Rect(bx, y, 24f, 20f), "<") && selectedIndex > 0) selectedIndex--;
            if (Widgets.ButtonText(new Rect(bx + 26f, y, 24f, 20f), ">") && selectedIndex < history.Count - 1) selectedIndex++;
            if (Widgets.ButtonText(new Rect(bx + 54f, y, 48f, 20f), paused ? "Resume" : "Pause")) paused = !paused;
            if (Widgets.ButtonText(new Rect(bx + 106f, y, 42f, 20f), "Clear")) { history.Clear(); selectedIndex = -1; }
            if (Widgets.ButtonText(new Rect(bx + 152f, y, 36f, 20f), "Hide")) minimized = true;

            y += 24f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            // Meta line
            DrawLine(ref y, inner, $"Shot {selectedIndex + 1}/{history.Count} (Burst {t.ShotIndexInBurst}/{t.BurstShotCount}) | {t.ProjectileDefName} | {t.TrajectoryWorkerName}", new Color(0.75f, 0.82f, 0.9f));
            DrawLine(ref y, inner, $"Range: {t.HorizontalRange:F1}c  EstDist: {t.EstimatedTargDist:F1}c  Speed: {t.ShotSpeed:F1} c/s  g: {t.GravityPerWidth:F2}", Color.white);
            DrawLine(ref y, inner, $"Height: Barrel {t.ShotHeight:F2} vc -> Target {t.TargetHeight:F2} vc (dH={t.TargetHeight - t.ShotHeight:+0.00;-0.00;0.00})", Color.white);

            y += 3f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            // Vectors
            DrawLine(ref y, inner, "VECTORS (Yaw: 0=N,90=E | Pitch: vs Horizon)", new Color(1f, 0.88f, 0.55f));
            DrawLine(ref y, inner, $"  Direct LoS Vector : Yaw {t.IdealYawDeg,6:F2} deg | Pitch {t.DirectLosPitchDeg,+6:F2} deg", new Color(0.8f, 0.8f, 0.8f));
            DrawLine(ref y, inner, $"  100% Acc Ballistic: Yaw {t.IdealYawDeg,6:F2} deg | Pitch {t.IdealBallisticPitchDeg,+6:F2} deg", new Color(0.55f, 1f, 0.65f));
            DrawLine(ref y, inner, $"  Actual Shell Out  : Yaw {t.ActualYawDeg,6:F2} deg | Pitch {t.ActualPitchDeg,+6:F2} deg", Color.white);

            float missAtRange = t.HorizontalRange * Mathf.Tan(t.TotalAngularErrorDeg * Mathf.Deg2Rad);
            Color errColor = t.TotalAngularErrorDeg < 0.25f ? new Color(0.45f, 1f, 0.55f)
                           : t.TotalAngularErrorDeg < 1.0f ? new Color(1f, 0.85f, 0.35f)
                           : new Color(1f, 0.42f, 0.42f);
            DrawLine(ref y, inner, $"  ERROR vs 100% Acc : dYaw {t.TotalDeltaYawDeg,+6:F3} deg | dPitch {t.TotalDeltaPitchDeg,+6:F3} deg | Total {t.TotalAngularErrorDeg:F3} deg (~{missAtRange:F2}c)", errColor);

            y += 3f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            // Contributors
            DrawLine(ref y, inner, "DEVIATION CONTRIBUTORS (dYaw / dPitch)", new Color(1f, 0.88f, 0.55f));
            DrawContrib(ref y, inner, "1. Aim/Target Shift", t.AimShiftYawDeg, t.AimShiftPitchDeg, $"dist±{t.DistShiftMax:F2}c circ {t.CircularMissRadius:F2}c vis {t.VisibilityShift:F2}c ind {t.IndirectFireShift:F2}c");
            DrawContrib(ref y, inner, "2. Sway (GetSwayVec)", t.SwayYawDeg, t.SwayPitchDeg, $"amp={t.SwayAmplitude:F2} deg, acc={t.ShootingAccuracy:F2}");
            DrawContrib(ref y, inner, "3. Recoil", t.RecoilYawDeg, t.RecoilPitchDeg, t.RecoilControlInfo ?? $"shot #{t.ShotIndexInBurst}");
            DrawContrib(ref y, inner, "4. Mech Spread", t.SpreadYawDeg, t.SpreadPitchDeg, $"spread={t.SpreadDegrees:F3} deg");
            if (Mathf.Abs(t.ClampYawDeg) > 0.0001f || Mathf.Abs(t.ClampPitchDeg) > 0.0001f)
            {
                DrawContrib(ref y, inner, "5. Clamping", t.ClampYawDeg, t.ClampPitchDeg, "TurretClampingExtension");
            }

            y += 3f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            DrawLine(ref y, inner, $"Stats: AimAcc={t.AimingAccuracy:F2} Sights={t.SightsEfficiency:F2} | Light={t.LightingShift:F2} Wthr={t.WeatherShift:F2} Smoke={t.SmokeDensity:F2}", new Color(0.75f, 0.78f, 0.85f));
            DrawLine(ref y, inner, $"LeadShift={t.LeadShift:F2}c | EstDist - ShotDist = {t.EstimatedTargDist - t.ShotDist:+0.00;-0.00;0.00}c", new Color(0.75f, 0.78f, 0.85f));

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private static void DrawLine(ref float y, Rect inner, string text, Color color)
        {
            GUI.color = color;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f), text);
            y += 17f;
        }

        private static void DrawContrib(ref float y, Rect inner, string label, float dYaw, float dPitch, string note)
        {
            float mag = Mathf.Abs(dYaw) + Mathf.Abs(dPitch);
            Color c = mag < 0.05f ? new Color(0.65f, 0.7f, 0.75f)
                    : mag < 0.5f ? new Color(1f, 0.9f, 0.45f)
                    : new Color(1f, 0.48f, 0.45f);
            GUI.color = c;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                $"  {label,-18}: {dYaw,+6:F3} / {dPitch,+6:F3} deg  ({note})");
            y += 17f;
        }
    }
}
