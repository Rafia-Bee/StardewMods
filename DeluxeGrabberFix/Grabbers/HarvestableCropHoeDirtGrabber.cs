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
        if (!Config.Features.harvestCrops)
            return false;

        if (feature is not HoeDirt dirt || dirt.crop == null)
            return false;

        string harvestId = dirt.crop.indexOfHarvest.Value;

        if (!string.IsNullOrEmpty(harvestId))
        {
            string qualifiedId = ItemRegistry.QualifyItemId(harvestId);
            if (qualifiedId != null && Config.IsItemExcluded(qualifiedId))
                return false;

            if (Config.Features.flowers != ModConfig.FlowerHarvestMode.All
                && ItemRegistry.Create<Object>(harvestId).Category == Object.flowersCategory)
            {
                if (Config.Features.flowers == ModConfig.FlowerHarvestMode.Off)
                    return false;
                if (Config.Features.flowers == ModConfig.FlowerHarvestMode.Smart && Helpers.IsFlowerNearBeeHouse(Location, tile, Config.Features.beeHouseRange))
                    return false;
            }
        }

        // Materialize once: this list is enumerated up to three times per tile
        // (the .Any() guard, TryAddItem's foreach, and TryAddItem's chest-full
        // report loop). Specialized mode's GetFilteredGrabberPairs adds a Where
        // over a Where, so re-evaluating is non-trivial on big farms.
        // Audit §2.10: GetGrabbersInRangeOfTile applies the range filter only to
        // same-location grabbers in global modes; cross-location grabbers tail the
        // list as a routing fallback so cache-order doesn't override "local first."
        var nearbyGrabbers = GetGrabbersInRangeOfTile(tile, Config.Features.harvestCropsRange, Config.Features.harvestCropsRangeMode).ToList();

        // No grabber in range to receive the harvest, leave the crop alone rather than
        // destroying it and dropping debris on the ground.
        if (nearbyGrabbers.Count == 0)
            return false;

        // Same idea when every grabber in range is full: leave the crop growing
        // instead of harvesting it just to drop it on the floor (issue #114).
        if (!AnyGrabberHasSpace(nearbyGrabbers))
            return false;

        // try/finally so a thrown harvest (third-party Harmony patch on Crop.harvest,
        // a malformed crop, etc.) doesn't leave HarvestInterceptor._intercepting=true
        // and trip audit 4.6's reentry throw on every subsequent grab.
        bool shouldDestroy;
        List<Item> items;
        HarvestInterceptor.BeginIntercept();
        try
        {
            shouldDestroy = dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, isForcedScytheHarvest: true);
        }
        finally
        {
            items = HarvestInterceptor.EndIntercept();
        }

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
