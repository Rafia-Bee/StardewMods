using HarmonyLib;
using StardewModdingAPI;

namespace MoreQuests.Framework;

/// <summary>
/// Harmony patches for the quest board to support multiple simultaneous quests
/// and custom quest generation.
/// </summary>
internal static class QuestBoardPatches
{
    private static IMonitor _monitor = null!;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;

        // TODO: Patch Billboard.SetUpQuests to replace vanilla quest generation
        // TODO: Patch Billboard.draw to render multiple quests
        // TODO: Patch Billboard.receiveLeftClick to handle clicking individual quests
    }
}
