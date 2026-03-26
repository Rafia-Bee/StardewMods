using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class HarvestableCropHoeDirtGrabber : TerrainFeaturesMapGrabber
{
    public HarvestableCropHoeDirtGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.harvestCrops)
            return false;

        if (feature is not HoeDirt dirt || dirt.crop == null)
            return false;

        if (Config.flowers != ModConfig.FlowerHarvestMode.All)
        {
            string harvestId = dirt.crop.indexOfHarvest.Value;
            if (!string.IsNullOrEmpty(harvestId) && ItemRegistry.Create<Object>(harvestId).Category == Object.flowersCategory)
            {
                if (Config.flowers == ModConfig.FlowerHarvestMode.Off)
                    return false;
                if (Config.flowers == ModConfig.FlowerHarvestMode.Smart && Helpers.IsFlowerNearBeeHouse(Location, tile, Config.beeHouseRange))
                    return false;
            }
        }

        var nearbyGrabbers = Helpers.GetNearbyObjectsToTile(tile, GrabberPairs, Config.harvestCropsRange, Config.harvestCropsRangeMode);

        HarvestInterceptor.BeginIntercept();
        bool shouldDestroy = dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, isForcedScytheHarvest: true);
        List<Item> items = HarvestInterceptor.EndIntercept();

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                if (!TryAddItem(item, nearbyGrabbers))
                    Game1.createItemDebris(item, new Vector2(tile.X * 64 + 32, tile.Y * 64 + 32), -1, Location);
            }

            if (shouldDestroy)
                dirt.destroyCrop(false);

            return true;
        }
        return false;
    }
}
