using System;
using static AbsolutelyMoreCannons.OpenTopGeometry;

internal static class Program
{
    private static int failures;

    private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

    private static void Check(bool ok, string name)
    {
        if (ok) return;
        failures++;
        Console.WriteLine("FAIL: " + name);
    }

    private static int Main()
    {
        RotateOffset(0, 1, 90, out var x, out var z);
        Check(Near(x, 1) && Near(z, 0), "fwd at East -> +X");
        RotateOffset(1, 0, 90, out x, out z);
        Check(Near(x, 0) && Near(z, -1), "right at East -> -Z (south)");
        RotateOffset(0.5f, -0.8f, 0, out x, out z);
        Check(Near(x, 0.5f) && Near(z, -0.8f), "0 deg identity");

        Check(Near(CompassAngle(1, 0), 90) && Near(CompassAngle(0, -1), 180), "compass");
        Check(Near(CompassAngle(-1, 0), 270), "compass west in [0,360)");
        Check(Near(DeltaAngle(350, 10), 20), "wraparound");

        // A shot travelling south (0,-1) comes FROM the north.
        Check(InShieldArc(0, -1, 0, 120), "front shot inside front arc");
        Check(!InShieldArc(0, 1, 0, 120), "rear shot outside front arc");
        Check(InShieldArc(-1, 0, 30, 120), "shot from east, top at 30, arc 120 (delta 60) inside");
        Check(!InShieldArc(-1, 0, 29, 118), "delta 61 > 59 outside");
        Check(InShieldArc(0, 1, 0, 360), "360 covers rear");
        Check(!InShieldArc(0, -1, 0, 0), "arc 0 = no shield");

        GunnerVerticalRange(1.1f, 1.75f, out var b, out var t);
        Check(Near(t, 1.1f) && Near(b, 0), "clamped at ground");
        GunnerVerticalRange(-1f, 1.0f, out b, out t);
        Check(Near(t, 1.45f) && Near(b, 0.45f), "fallback 1.45");

        Check(RemainingPenetration(12, 8) > 0 && RemainingPenetration(8, 8) <= 0, "penetration");
        Check(PenetratesShield(true, 12, 8) && !PenetratesShield(true, 8, 8), "sharp rounds penetrate only above shield armour");
        Check(!PenetratesShield(false, 50, 8), "blunt-only rounds never penetrate the shield");

        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
