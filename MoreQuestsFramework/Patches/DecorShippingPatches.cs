using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using SObject = StardewValley.Object;

namespace MoreQuestsFramework.Patches;

// Flips canBeShipped to true while an active quest opts into decor shipping, so
// festival-supply quests (Moonlight Jellies, Luau, etc.) can ship items vanilla
// otherwise refuses (Hay Bales, Wood Lamp-posts, Tubs of Flowers). The override
// is scoped to the specific item ids the quest declared, so unrelated decor stays
// blocked from the bin.
internal static class DecorShippingPatches
{
    public static int ActiveCount;

    private static readonly List<Func<SObject, bool>> Predicates = new();

    public static void SetPredicates(IEnumerable<Func<SObject, bool>> predicates)
    {
        Predicates.Clear();
        if (predicates == null) return;
        foreach (var p in predicates)
            if (p != null) Predicates.Add(p);
    }

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        var canBeShipped = AccessTools.Method(typeof(SObject), nameof(SObject.canBeShipped));
        if (canBeShipped == null)
        {
            monitor.Log("DecorShippingPatches: Object.canBeShipped not found; decor-shipping bypass inactive.", LogLevel.Warn);
            return;
        }
        harmony.Patch(
            original: canBeShipped,
            postfix: new HarmonyMethod(typeof(DecorShippingPatches), nameof(CanBeShipped_Postfix)));
    }

    public static void CanBeShipped_Postfix(SObject __instance, ref bool __result)
    {
        if (__result || ActiveCount <= 0 || __instance == null || Predicates.Count == 0)
            return;
        for (int i = 0; i < Predicates.Count; i++)
        {
            if (Predicates[i](__instance))
            {
                __result = true;
                return;
            }
        }
    }
}
