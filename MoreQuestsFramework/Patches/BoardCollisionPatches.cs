using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Posting.Boards;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Without this, board sprites read as a "roof" the player can walk under.
// Collision footprint matches the exact pixel rect that BoardWorldRenderer draws.
internal static class BoardCollisionPatches
{
    private static BoardRegistry _registry = null!;
    private static IModRegistry? _modRegistry;

    public static void Apply(Harmony harmony, BoardRegistry registry, IModRegistry modRegistry)
    {
        _registry = registry;
        _modRegistry = modRegistry;
        var method = AccessTools.Method(
            typeof(GameLocation),
            nameof(GameLocation.isCollidingPosition),
            new[]
            {
                typeof(Rectangle),
                typeof(xTile.Dimensions.Rectangle),
                typeof(bool),
                typeof(int),
                typeof(bool),
                typeof(Character),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)
            });
        harmony.Patch(
            original: method,
            postfix: new HarmonyMethod(typeof(BoardCollisionPatches), nameof(IsColliding_Postfix)));
    }

    public static void IsColliding_Postfix(GameLocation __instance, Rectangle position, ref bool __result)
    {
        if (__result)
            return;
        if (__instance == null || string.IsNullOrEmpty(__instance.Name))
            return;
        foreach (var board in _registry.InLocation(__instance.Name))
        {
            // Sprite hides when Available fails, so blocking the tile would leave the
            // player walking into an invisible wall.
            if (!IsAvailable(board))
                continue;
            var footprint = BoardWorldRenderer.GetSpriteRect(board);
            if (position.Intersects(footprint))
            {
                __result = true;
                return;
            }
        }
    }

    private static bool IsAvailable(Api.BoardDefinition board)
    {
        if (board.Available == null || board.Available.Count == 0)
            return true;
        return ConditionEvaluator.Evaluate(board.Available, _modRegistry);
    }
}
