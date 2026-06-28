using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using DeluxeGrabberFix.Framework;
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

        if (obj is not IndoorPot pot)
            return false;

        // Tea (and other ready bushes) planted in a garden pot. The crop path
        // below only covers seeded crops like coffee, so a potted tea bush would
        // otherwise never get collected (issue #115).
        if (pot.bush.Value != null)
            return TryGrabPotBush(tile, pot.bush.Value);

        if (pot.hoeDirt.Value?.crop == null)
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

        // try/finally so a thrown harvest doesn't leave HarvestInterceptor._intercepting=true
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

            // The IndoorPot stays in location.Objects (only its inner crop is
            // cleared), so mark the tile so subsequent ObjectsMapGrabber-derived
            // grabbers in the same cycle skip it. Matches GenericObjectGrabber's
            // convention; without this the skip relies on bigCraftable + null-crop
            // short-circuits firing in the right order.
            Mod.GrabbedTiles?.Add(tile);

            return true;
        }
        return false;
    }

    private bool TryGrabPotBush(Vector2 tile, Bush bush)
    {
        if (!BushHarvest.TryGetHarvest(Mod, bush, tile, Location, out var items, out int exp))
            return false;

        var nearbyGrabbers = GetGrabbersInRangeOfTile(tile, Config.Features.harvestCropsRange, Config.Features.harvestCropsRangeMode).ToList();

        // No grabber in range, or every chest in range is full: leave the tea on
        // the bush for the next pass rather than shaking it off onto the floor.
        if (nearbyGrabbers.Count == 0 || !AnyGrabberHasSpace(nearbyGrabbers))
            return false;

        if (TryAddItems(items, nearbyGrabbers))
        {
            bush.tileSheetOffset.Value = 0;
            bush.setUpSourceRect();
            GainExperience(2, exp);
            Mod.GrabbedTiles?.Add(tile);
            return true;
        }
        return false;
    }
}
