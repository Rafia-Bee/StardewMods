using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class FarmCaveMushroomGrabber : ObjectsMapGrabber
{
    private readonly Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetMushroomHarvest;

    public FarmCaveMushroomGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        GetMushroomHarvest = Mod.Api.GetMushroomHarvest ?? DefaultGetMushroomHarvest;
    }

    private KeyValuePair<Object, int> DefaultGetMushroomHarvest(Object mushroom, Vector2 mushroomBoxTile, GameLocation location)
    {
        mushroom.Quality = 0;
        return new KeyValuePair<Object, int>(mushroom, 0);
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.farmCaveMushrooms || obj.ParentSheetIndex != 128 || obj.heldObject.Value == null)
            return false;

        var harvest = GetMushroomHarvest(obj.heldObject.Value, tile, Location);
        if (TryAddItem(harvest.Key))
        {
            obj.heldObject.Value = null;
            GainExperience(2, harvest.Value);
            return true;
        }
        return false;
    }
}
