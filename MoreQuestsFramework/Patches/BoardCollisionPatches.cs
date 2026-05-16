using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreQuestsFramework.Registry;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Without this, board sprites read as a "roof" the player can walk under.
internal static class BoardCollisionPatches
{
    private const int TilePixels = 64;
    private static BoardRegistry _registry = null!;

    public static void Apply(Harmony harmony, BoardRegistry registry)
    {
        _registry = registry;
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
            var footprint = new Rectangle(
                board.TileX * TilePixels + board.DrawOffsetX,
                board.TileY * TilePixels + board.DrawOffsetY,
                board.FootprintWidth * TilePixels,
                board.FootprintHeight * TilePixels);
            if (position.Intersects(footprint))
            {
                __result = true;
                return;
            }
        }
    }
}
