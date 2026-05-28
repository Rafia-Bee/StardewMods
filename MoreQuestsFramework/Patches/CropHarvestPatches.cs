using System;
using HarmonyLib;
using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;

namespace MoreQuestsFramework.Patches;

// Fires the framework's CropHarvested event for any successful harvest (player or
// Junimo, grab or scythe, single-harvest or multi-harvest regrowth). Multi-harvest
// perennials (Hops, Blueberry, Cranberries, Hot Pepper, etc.) return `false` from
// Crop.harvest on success and signal it by mutating dayOfCurrentPhase from <= 0
// (ready) to RegrowDays (regrowing), so we capture the pre-call value in a prefix
// and detect success in the postfix by either __result or the state delta.
internal static class CropHarvestPatches
{
    private static IMonitor? _monitor;

    public static event Action<CropHarvestInfo>? CropHarvested;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
            prefix: new HarmonyMethod(typeof(CropHarvestPatches), nameof(Harvest_Prefix)),
            postfix: new HarmonyMethod(typeof(CropHarvestPatches), nameof(Harvest_Postfix)));
    }

    public static void Harvest_Prefix(Crop __instance, ref int __state)
    {
        __state = __instance?.dayOfCurrentPhase.Value ?? int.MinValue;
    }

    public static void Harvest_Postfix(
        Crop __instance,
        int xTile,
        int yTile,
        HoeDirt soil,
        JunimoHarvester junimoHarvester,
        bool isForcedScytheHarvest,
        bool __result,
        int __state)
    {
        if (__instance == null)
            return;
        bool regrewAfterPick = __state <= 0 && __instance.dayOfCurrentPhase.Value > 0;
        if (!__result && !regrewAfterPick)
            return;
        try
        {
            string harvestId = __instance.indexOfHarvest.Value;
            if (string.IsNullOrEmpty(harvestId))
                return;
            string qualified = harvestId.StartsWith("(", StringComparison.Ordinal)
                ? harvestId
                : "(O)" + harvestId;
            string loc = soil?.Location?.NameOrUniqueName
                ?? Game1.currentLocation?.NameOrUniqueName
                ?? string.Empty;
            var info = new CropHarvestInfo(qualified, loc, xTile, yTile, junimoHarvester != null);
            CropHarvested?.Invoke(info);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"CropHarvested handler threw: {ex.Message}", LogLevel.Warn);
        }
    }
}

