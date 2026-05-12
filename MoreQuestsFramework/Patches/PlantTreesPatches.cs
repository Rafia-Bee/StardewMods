using HarmonyLib;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

/// Harmony postfix on `GameLocation.CanPlantTreesHere` so an active PlantTrees AdventureQuest
/// can opt its target location into wild-tree-seed planting even when vanilla rules say no.
/// Vanilla already allows wild seeds on outdoor Dirt tiles, so this is only load-bearing for
/// target locations the player is routed to that lack Dirt back-layer tiles (Town etc.). The
/// per-tile gates (`IsNoSpawnTile`, object/terrain collision, `CheckItemPlantRules` diggable
/// check) still apply, so the player must still find a plantable tile within the location.
///
/// Skipped entirely when the AnythingAnywhere mod (`Espy.AnythingAnywhere`) is loaded - that
/// mod already patches placement validation, so layering our patch on top would be redundant
/// and risks double-counting placement rules.
internal static class PlantTreesPatches
{
    private static IModRegistry? _registry;
    private static bool _anywhereLoaded;

    public static void Apply(Harmony harmony, IModRegistry registry)
    {
        _registry = registry;
        _anywhereLoaded = registry.IsLoaded("Espy.AnythingAnywhere");

        harmony.Patch(
            original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.CanPlantTreesHere)),
            postfix: new HarmonyMethod(typeof(PlantTreesPatches), nameof(CanPlantTreesHere_Postfix)));
    }

    public static void CanPlantTreesHere_Postfix(GameLocation __instance, string itemId, ref bool __result)
    {
        if (__result)
            return;
        if (_anywhereLoaded)
            return;
        if (!Object.isWildTreeSeed(itemId))
            return;
        if (Game1.player?.questLog == null)
            return;

        string locationName = __instance?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(locationName))
            return;

        foreach (var quest in Game1.player.questLog)
        {
            if (quest is AdventureQuest aq && aq.HasActiveStepTargeting(AdventureStepKind.Plant, locationName))
            {
                __result = true;
                return;
            }
        }
    }
}
