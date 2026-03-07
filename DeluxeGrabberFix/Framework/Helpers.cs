using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
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
}
