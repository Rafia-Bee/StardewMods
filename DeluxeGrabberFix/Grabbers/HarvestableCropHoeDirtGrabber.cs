using System.Collections.Generic;
using System.Linq;
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

        string harvestId = dirt.crop.indexOfHarvest.Value;

        if (!string.IsNullOrEmpty(harvestId))
        {
            string qualifiedId = ItemRegistry.QualifyItemId(harvestId);
            if (qualifiedId != null && Config.IsItemExcluded(qualifiedId))
                return false;

            if (Config.flowers != ModConfig.FlowerHarvestMode.All
                && ItemRegistry.Create<Object>(harvestId).Category == Object.flowersCategory)
            {
                if (Config.flowers == ModConfig.FlowerHarvestMode.Off)
                    return false;
                if (Config.flowers == ModConfig.FlowerHarvestMode.Smart && Helpers.IsFlowerNearBeeHouse(Location, tile, Config.beeHouseRange))
                    return false;
            }
        }

        // Materialize once: this list is enumerated up to three times per tile
        // (the .Any() guard, TryAddItem's foreach, and TryAddItem's chest-full
        // report loop). Specialized mode's GetFilteredGrabberPairs adds a Where
        // over a Where, so re-evaluating is non-trivial on big farms.
        var nearbyGrabbers = Helpers.GetNearbyObjectsToTile(tile, GetFilteredGrabberPairs(), Config.harvestCropsRange, Config.harvestCropsRangeMode).ToList();

        // No grabber in range to receive the harvest, leave the crop alone rather than
        // destroying it and dropping debris on the ground.
        if (nearbyGrabbers.Count == 0)
            return false;

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
            {
                dirt.destroyCrop(false);
                Mod.ReportCropsHarvested(Location);
            }

            return true;
        }
        return false;
    }
}
