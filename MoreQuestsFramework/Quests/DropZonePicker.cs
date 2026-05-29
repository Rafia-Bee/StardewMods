using System;
using Microsoft.Xna.Framework;
using StardewValley;

namespace MoreQuestsFramework.Quests;

// Picks a clear tile for a ritual-circle drop zone. Strictly read-only: it queries tile
// state (passable, not water, nothing placed) to choose a center, and never writes to the
// location. So it can't destroy decor, chests, machines, or mine reward chests, and the
// circle the renderer draws around the returned tile is purely visual (no collision).
// Public so content mods can pre-pick an overground zone center at quest-accept time.
public static class DropZonePicker
{
    public static bool TryPickZone(GameLocation loc, int radius, int minDistFromWarp, int attempts, out Point center)
    {
        center = Point.Zero;
        if (loc?.Map?.Layers == null || loc.Map.Layers.Count == 0)
            return false;
        var layer = loc.Map.Layers[0];
        int w = layer.LayerWidth;
        int h = layer.LayerHeight;
        if (w <= 4 || h <= 4)
            return false;

        for (int i = 0; i < attempts; i++)
        {
            int x = Game1.random.Next(2, w - 2);
            int y = Game1.random.Next(2, h - 2);
            // Center + its 4 neighbours must be standable (room to stand and drop). We don't
            // require the whole disc clear: mine floors are too rocky and the ring is cosmetic.
            if (!IsStandable(loc, x, y)) continue;
            if (!IsStandable(loc, x + 1, y) || !IsStandable(loc, x - 1, y)) continue;
            if (!IsStandable(loc, x, y + 1) || !IsStandable(loc, x, y - 1)) continue;
            if (TooCloseToWarp(loc, x, y, minDistFromWarp)) continue;

            center = new Point(x, y);
            return true;
        }
        return false;
    }

    private static bool IsStandable(GameLocation loc, int x, int y)
    {
        if (loc.isWaterTile(x, y))
            return false;
        // Read-only: true only on a free, walkable tile (no object/clump/terrain/building).
        return loc.CanItemBePlacedHere(new Vector2(x, y));
    }

    private static bool TooCloseToWarp(GameLocation loc, int x, int y, int minDist)
    {
        if (minDist <= 0 || loc.warps == null)
            return false;
        foreach (var warp in loc.warps)
        {
            int dx = warp.X - x;
            int dy = warp.Y - y;
            if (dx * dx + dy * dy < minDist * minDist)
                return true;
        }
        return false;
    }
}
