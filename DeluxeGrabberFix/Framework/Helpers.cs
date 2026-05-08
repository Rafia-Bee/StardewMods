using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal static class Helpers
{
    public static IEnumerable<KeyValuePair<Vector2, Object>> GetNearbyObjectsToTile(
        Vector2 tile,
        IEnumerable<KeyValuePair<Vector2, Object>> objects,
        int range,
        ModConfig.HarvestCropsRangeMode rangeMode)
    {
        if (range > -1)
        {
            return rangeMode switch
            {
                ModConfig.HarvestCropsRangeMode.Walk => objects.Where(pair =>
                {
                    Vector2 key = pair.Key;
                    float dist = Math.Abs(tile.X - key.X) + Math.Abs(tile.Y - key.Y);
                    return dist <= range;
                }),
                ModConfig.HarvestCropsRangeMode.Square => objects.Where(pair =>
                {
                    Vector2 key = pair.Key;
                    return tile.X >= key.X - range && tile.X <= key.X + range
                        && tile.Y >= key.Y - range && tile.Y <= key.Y + range;
                }),
                _ => throw new Exception($"Unexpected range mode {rangeMode}.")
            };
        }
        return objects;
    }

    // Audit §2.10: stable two-bucket partition that keeps same-location grabbers ahead
    // of cross-location ones in the resulting list. Used by MapGrabber so global-mode
    // routing prefers a same-map grabber when one exists, falling back to cross-map
    // entries only as the cache-order tail. Pure helper so the partition can be tested
    // without standing up a GameLocation -- the caller passes the predicate.
    public static List<KeyValuePair<Vector2, Object>> SortSameLocationFirst(
        IEnumerable<KeyValuePair<Vector2, Object>> source,
        Func<KeyValuePair<Vector2, Object>, bool> isSameLocation)
    {
        var same = new List<KeyValuePair<Vector2, Object>>();
        var cross = new List<KeyValuePair<Vector2, Object>>();
        foreach (var pair in source)
        {
            if (isSameLocation(pair)) same.Add(pair);
            else cross.Add(pair);
        }
        same.AddRange(cross);
        return same;
    }

    // Audit §2.10: tile coordinates aren't comparable across maps in global modes,
    // so the range filter is meaningful only against same-location grabbers.
    // Cross-location grabbers pass through unfiltered as a routing fallback so a
    // global cache still receives items from a tile when no same-map grabber is
    // in range. Returns same-in-range first, then cross-location. Pure helper.
    public static List<KeyValuePair<Vector2, Object>> GetGrabbersInRangeOrCrossLocation(
        Vector2 tile,
        IEnumerable<KeyValuePair<Vector2, Object>> grabbers,
        int range,
        ModConfig.HarvestCropsRangeMode rangeMode,
        Func<KeyValuePair<Vector2, Object>, bool> isSameLocation)
    {
        var sameLocal = new List<KeyValuePair<Vector2, Object>>();
        var crossLocal = new List<KeyValuePair<Vector2, Object>>();
        foreach (var pair in grabbers)
        {
            if (isSameLocation(pair)) sameLocal.Add(pair);
            else crossLocal.Add(pair);
        }

        var result = (range <= -1)
            ? sameLocal
            : GetNearbyObjectsToTile(tile, sameLocal, range, rangeMode).ToList();
        result.AddRange(crossLocal);
        return result;
    }

    public static Object SetForageStatsBasedOnProfession(Farmer player, Object forageable, Vector2 tileSpawned, bool ignoreGatherer = false)
    {
        Random random = new((int)Game1.uniqueIDForThisGame / 2 + (int)Game1.stats.DaysPlayed + (int)tileSpawned.X + (int)tileSpawned.Y * 777);

        if (player.professions.Contains(ProfessionIds.Botanist))
        {
            forageable.Quality = 4;
        }
        else if (random.NextDouble() < (double)(player.ForagingLevel / 30f))
        {
            forageable.Quality = 2;
        }
        else if (random.NextDouble() < (double)(player.ForagingLevel / 15f))
        {
            forageable.Quality = 1;
        }

        if (!ignoreGatherer && player.professions.Contains(ProfessionIds.Gatherer) && random.NextDouble() < 0.2)
        {
            forageable.Stack++;
        }

        return forageable;
    }

    private static GameLocation _cachedBeeHouseLocation;
    private static int _cachedBeeHouseTick;
    private static List<Vector2> _cachedBeeHousePositions;

    public static bool IsFlowerNearBeeHouse(GameLocation location, Vector2 flowerTile, int range)
    {
        var positions = GetBeeHousePositions(location);
        foreach (var pos in positions)
        {
            float dist = Math.Abs(flowerTile.X - pos.X) + Math.Abs(flowerTile.Y - pos.Y);
            if (dist <= range)
                return true;
        }
        return false;
    }

    private static List<Vector2> GetBeeHousePositions(GameLocation location)
    {
        if (_cachedBeeHouseLocation == location && _cachedBeeHouseTick == Game1.ticks)
            return _cachedBeeHousePositions;

        var positions = new List<Vector2>();
        foreach (var pair in location.Objects.Pairs)
        {
            var obj = pair.Value;
            if (obj.QualifiedItemId == BigCraftableIds.BeeHouse
                || (obj.QualifiedItemId?.StartsWith(BigCraftableIds.MpsPrefix) == true
                    && obj.QualifiedItemId.Contains("BeeHouse")))
            {
                positions.Add(pair.Key);
            }
        }

        _cachedBeeHouseLocation = location;
        _cachedBeeHouseTick = Game1.ticks;
        _cachedBeeHousePositions = positions;

        return positions;
    }
}
