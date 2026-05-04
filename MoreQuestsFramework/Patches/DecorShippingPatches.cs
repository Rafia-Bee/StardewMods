using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

/// Harmony postfix on `StardewValley.Object.canBeShipped` that flips the result to true
/// while at least one quest in the active log opts into furniture / decor shipping.
/// Used by festival-supply quests (Moonlight Jellies, Egg Festival, Luau, Spirit's Eve,
/// East Scarp Spirit's Eve, Ridgeside Gathering, Fair) which ask the player to ship
/// items vanilla otherwise refuses (Hay Bales, Wood Lamp-posts, Tubs of Flowers).
///
/// Gated per §8.1: when no decor-shipping quest is in the log, `ActiveCount` is zero and
/// the postfix returns immediately without touching `__result`. Counter is recomputed
/// from the player's quest log every second by `ModEntry.RecomputeDecorShippingCount`,
/// so accept / complete / abandon all flow through the same diff without each quest
/// kind needing to opt in to a different bookkeeping callback.
///
/// Important: vanilla `Furniture.canBeShipped` does not exist as an override (Furniture
/// IS an Object), but `canBeShipped` itself returns false for any item with `bigCraftable`,
/// `Type == "Crafting"`, or various per-id checks. Forcing the postfix to true while a
/// decor-shipping quest is active lets the player deposit any of those items into the
/// bin without having to enumerate the full vanilla blocklist.
internal static class DecorShippingPatches
{
    /// Number of quests currently in the player's active log that opt into decor
    /// shipping. Postfix fast-paths out when zero.
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
