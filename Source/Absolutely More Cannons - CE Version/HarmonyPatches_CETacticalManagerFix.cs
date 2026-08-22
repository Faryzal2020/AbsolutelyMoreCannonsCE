using System;
using HarmonyLib;
using Verse;
using CombatExtended;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches to fix baseline Combat Extended NullReferenceExceptions
    /// when CompTacticalManager or CompSuppressable tick on dead pawns or corpses.
    /// </summary>
    [HarmonyPatch(typeof(CompTacticalManager), nameof(CompTacticalManager.CompTickRare))]
    public static class Patch_CompTacticalManager_CompTickRare
    {
        [HarmonyPrefix]
        public static bool Prefix(CompTacticalManager __instance)
        {
            if (__instance == null || __instance.parent == null) return false;

            if (__instance.parent is Corpse) return false;
            if (__instance.parent is Pawn pawn && (pawn.Dead || pawn.Destroyed || !pawn.Spawned)) return false;

            return true;
        }
    }

    /// <summary>
    /// Harmony prefix patch on CompSuppressable.IsHunkering getter.
    /// Prevents NullReferenceExceptions when checking hunkering state on dead pawns or corpses.
    /// </summary>
    [HarmonyPatch(typeof(CompSuppressable), nameof(CompSuppressable.IsHunkering), MethodType.Getter)]
    public static class Patch_CompSuppressable_IsHunkering
    {
        [HarmonyPrefix]
        public static bool Prefix(CompSuppressable __instance, ref bool __result)
        {
            if (__instance == null || __instance.parent == null || __instance.parent is Corpse)
            {
                __result = false;
                return false; // Skip original property getter
            }

            if (__instance.parent is Pawn pawn && (pawn.Dead || pawn.Destroyed || !pawn.Spawned))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
