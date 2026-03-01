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

        List<Object> items = Helpers.HarvestCropFromHoeDirt(Player, dirt, tile, !Config.flowers, out int exp);
        var nearbyGrabbers = Helpers.GetNearbyObjectsToTile(tile, GrabberPairs, Config.harvestCropsRange, Config.harvestCropsRangeMode);

        if (TryAddItems((IEnumerable<Item>)items, nearbyGrabbers))
        {
            if (!dirt.crop.RegrowsAfterHarvest())
            {
                dirt.destroyCrop(false);
            }
            else
            {
                dirt.crop.fullyGrown.Value = true;
                dirt.crop.dayOfCurrentPhase.Value = dirt.crop.GetData()?.RegrowDays ?? -1;
            }
            GainExperience(0, exp);
            return true;
        }
        return false;
    }
}
