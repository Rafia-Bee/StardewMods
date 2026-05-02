using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using DeluxeGrabberFix.Framework;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class IndoorPotGrabber : ObjectsMapGrabber
{
    public IndoorPotGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.Features.harvestCrops || !Config.Features.harvestCropsIndoorPots)
            return false;

        if (obj is not IndoorPot pot || pot.hoeDirt.Value?.crop == null)
            return false;

        HoeDirt dirt = pot.hoeDirt.Value;

        if (Config.Features.flowers != ModConfig.FlowerHarvestMode.All)
        {
            string harvestId = dirt.crop.indexOfHarvest.Value;
            if (!string.IsNullOrEmpty(harvestId) && ItemRegistry.Create<Object>(harvestId).Category == Object.flowersCategory)
            {
                if (Config.Features.flowers == ModConfig.FlowerHarvestMode.Off)
                    return false;
                if (Config.Features.flowers == ModConfig.FlowerHarvestMode.Smart && Helpers.IsFlowerNearBeeHouse(Location, tile, Config.Features.beeHouseRange))
                    return false;
            }
        }

        // Materialize once: this list is enumerated up to three times per pot
        // (the .Count guard, TryAddItem's foreach, and TryAddItem's chest-full
        // report loop). Specialized mode's GetFilteredGrabberPairs adds a Where
        // over a Where, so re-evaluating is non-trivial on big farms.
        var nearbyGrabbers = Helpers.GetNearbyObjectsToTile(tile, GetFilteredGrabberPairs(), Config.Features.harvestCropsRange, Config.Features.harvestCropsRangeMode).ToList();

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
