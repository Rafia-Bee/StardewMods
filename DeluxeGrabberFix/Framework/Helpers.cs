using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

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

        if (player.professions.Contains(16))
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

        if (!ignoreGatherer && player.professions.Contains(13) && random.NextDouble() < 0.2)
        {
            forageable.Stack++;
        }

        return forageable;
    }

    public static List<Object> HarvestCropFromHoeDirt(Farmer player, HoeDirt dirt, Vector2 tile, bool excludeFlowers, out int exp)
    {
        exp = 0;
        Crop crop = dirt.crop;
        List<Object> list = new();

        if (crop.currentPhase.Value < crop.phaseDays.Count - 1
            || (crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value > 0))
        {
            return list;
        }

        string harvestId = crop.indexOfHarvest.Value;

        if (harvestId == 73.ToString())
            return list;

        if (excludeFlowers && ItemRegistry.Create<Object>(harvestId).Category == -80)
            return list;

        Random random = new((int)tile.X * 7 + (int)tile.Y * 11 + (int)Game1.stats.DaysPlayed + (int)Game1.uniqueIDForThisGame);

        int fertilizerQualityBoostLevel = dirt.GetFertilizerQualityBoostLevel();
        double qualityChance = 0.2 * (player.FarmingLevel / 10.0) + 0.2 * fertilizerQualityBoostLevel * ((player.FarmingLevel + 2.0) / 12.0) + 0.01;
        double silverChance = Math.Min(0.75, qualityChance * 2.0);

        int quality = 0;
        if (fertilizerQualityBoostLevel >= 3 && random.NextDouble() < qualityChance / 2.0)
            quality = 4;
        else if (random.NextDouble() < qualityChance)
            quality = 2;
        else if (random.NextDouble() < silverChance || fertilizerQualityBoostLevel >= 3)
            quality = 1;

        if (harvestId == 771.ToString() || harvestId == 889.ToString())
            quality = 0;

        int stackSize = 1;
        CropData data = crop.GetData();

        if (data != null)
        {
            int minStack = data.HarvestMinStack;
            int maxStack = Math.Max(minStack, data.HarvestMaxStack);

            if (data.HarvestMaxIncreasePerFarmingLevel > 0f)
            {
                maxStack += (int)(Game1.player.FarmingLevel * data.HarvestMaxIncreasePerFarmingLevel);
            }

            if (minStack > 1 || maxStack > 1)
            {
                stackSize = random.Next(minStack, maxStack + 1);
            }
        }

        if (data != null && data.ExtraHarvestChance > 0.0)
        {
            while (random.NextDouble() < Math.Min(0.9, data.ExtraHarvestChance))
                stackSize++;
        }

        Object primaryItem;
        if (crop.programColored.Value)
        {
            primaryItem = new ColoredObject(harvestId, 1, crop.tintColor.Value) { Quality = quality };
        }
        else
        {
            primaryItem = ItemRegistry.Create<Object>(harvestId, 1, quality);
        }
        list.Add(primaryItem);

        if ((int)crop.GetHarvestMethod() != 1
            && random.NextDouble() < player.team.AverageLuckLevel() / 1500.0
                + player.team.AverageDailyLuck() / 1200.0 + 9.999999747378752E-05)
        {
            stackSize *= 2;
        }

        if (harvestId == 421.ToString())
        {
            harvestId = 431.ToString();
            stackSize = random.Next(1, 4);
        }

        Object extraItem;
        if (crop.programColored.Value)
        {
            extraItem = new ColoredObject(harvestId, 1, crop.tintColor.Value);
        }
        else
        {
            extraItem = ItemRegistry.Create<Object>(harvestId);
        }
        extraItem.Stack = stackSize - 1;

        int price = Game1.objectData[harvestId].Price;
        exp = (int)Math.Round(16.0 * Math.Log(0.018 * price + 1.0, Math.E));

        list.Add(extraItem);

        if (harvestId == 262.ToString() && random.NextDouble() < 0.4)
        {
            list.Add(ItemRegistry.Create<Object>(178.ToString()));
        }
        else if (harvestId == 771.ToString() && random.NextDouble() < 0.1)
        {
            list.Add(ItemRegistry.Create<Object>(770.ToString()));
        }

        return list;
    }
}
