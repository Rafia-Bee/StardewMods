using HarmonyLib;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Opts the target location into wild-tree-seed planting even where vanilla says no.
// Skipped when Espy.AnythingAnywhere is loaded to avoid double-counting placement rules.
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
