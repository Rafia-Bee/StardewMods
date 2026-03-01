using System.Collections.Generic;
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
        if (!Config.harvestCrops || !Config.harvestCropsIndoorPots)
            return false;

        if (obj is not IndoorPot pot || pot.hoeDirt.Value.crop == null)
            return false;

        HoeDirt dirt = pot.hoeDirt.Value;
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
