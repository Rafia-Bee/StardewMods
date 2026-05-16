using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Flips canBeShipped to true while any active quest opts into decor shipping, so
// festival-supply quests (Moonlight Jellies, Luau, etc.) can ship items vanilla
// otherwise refuses (Hay Bales, Wood Lamp-posts, Tubs of Flowers).
internal static class DecorShippingPatches
{
    // Recomputed every second by ModEntry; fast-paths out when zero.
    public static int ActiveCount;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        var canBeShipped = AccessTools.Method(typeof(Object), nameof(Object.canBeShipped));
        if (canBeShipped == null)
        {
            monitor.Log("DecorShippingPatches: Object.canBeShipped not found; decor-shipping bypass inactive.", LogLevel.Warn);
            return;
        }
        harmony.Patch(
            original: canBeShipped,
            postfix: new HarmonyMethod(typeof(DecorShippingPatches), nameof(CanBeShipped_Postfix)));
    }

    public static void CanBeShipped_Postfix(ref bool __result)
    {
        if (__result || ActiveCount <= 0)
            return;
        __result = true;
    }
}
