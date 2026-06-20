using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests;

/// The break-in beat of Story.PierreDontGetCaught.
internal static class JojaBreakInPatches
{
    private const int WindowOpen = 2400;
    private const int WindowClose = 2600;

    internal static void Apply(Harmony harmony, IMonitor monitor)
    {
        var target = AccessTools.Method(typeof(GameLocation), "lockedDoorWarp");
        if (target == null)
        {
            monitor.Log("Couldn't find GameLocation.lockedDoorWarp; Joja break-in disabled.", LogLevel.Warn);
            return;
        }
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(JojaBreakInPatches), nameof(BeforeLockedDoorWarp)));
    }

    private static bool BeforeLockedDoorWarp(GameLocation __instance, Point tile, string locationName)
    {
        if (!string.Equals(locationName, "JojaMart", StringComparison.Ordinal))
            return true;
        if (Game1.timeOfDay < WindowOpen || Game1.timeOfDay >= WindowClose)
            return true;
        if (!BreakInStepActive())
            return true;

        Game1.player.completelyStopAnimatingOrDoingAction();
        Game1.playSound("doorClose");
        Game1.warpFarmer(locationName, tile.X, tile.Y, flip: false);
        MarkBreakInDone();
        return false;
    }

    private static bool BreakInStepActive()
    {
        // Door stays open through the whole in-Joja phase: while breaking in and while there
        // are still pickles to stock. The sign and lay-low beats happen outside Joja.
        return HasActiveStep(ModEntry.PierreBreakInHandler) || HasActiveStep(ModEntry.PierreStockHandler);
    }

    private static bool HasActiveStep(string handler)
    {
        var scope = ModEntry.ModScope;
        if (scope == null)
            return false;
        foreach (var handle in scope.GetActiveCustomSteps(handler))
        {
            if (handle.IsActive)
                return true;
        }
        return false;
    }

    private static void MarkBreakInDone()
    {
        var scope = ModEntry.ModScope;
        if (scope == null)
            return;
        foreach (var handle in scope.GetActiveCustomSteps(ModEntry.PierreBreakInHandler))
            handle.MarkDone();
    }
}
