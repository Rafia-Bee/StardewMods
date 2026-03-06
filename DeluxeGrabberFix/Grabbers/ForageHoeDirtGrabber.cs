using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class ForageHoeDirtGrabber : TerrainFeaturesMapGrabber
{
    public ForageHoeDirtGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.forage || !Mod.IsForageGrabEnabled || feature is not HoeDirt dirt || !IsForageableHoeDirt(feature))
            return false;

        // Try crop.harvest() first (handles spring onions and any modded forage crops)
        HarvestInterceptor.BeginIntercept();
        bool shouldDestroy = dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, isForcedScytheHarvest: true);
        List<Item> items = HarvestInterceptor.EndIntercept();

        // If harvest() didn't produce anything, try hitWithHoe() (handles ginger)
        if (items.Count == 0)
        {
            HarvestInterceptor.BeginIntercept();
            shouldDestroy = dirt.crop.hitWithHoe((int)tile.X, (int)tile.Y, Location, dirt);
            items = HarvestInterceptor.EndIntercept();
        }

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                if (!TryAddItem(item))
                    Game1.createItemDebris(item, new Vector2(tile.X * 64 + 32, tile.Y * 64 + 32), -1, Location);
            }

            if (shouldDestroy)
                dirt.destroyCrop(false);

            return true;
        }
        return false;
    }

    private bool IsForageableHoeDirt(TerrainFeature feature)
    {
        return feature is HoeDirt dirt
            && dirt.crop != null
            && dirt.crop.forageCrop.Value;
    }
}
