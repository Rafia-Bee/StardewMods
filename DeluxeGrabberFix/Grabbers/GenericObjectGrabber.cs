using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.Locations;

namespace DeluxeGrabberFix.Grabbers;

internal class GenericObjectGrabber : ObjectsMapGrabber
{
    private static readonly HashSet<int> ForageableItems = new() { 430 };

    public GenericObjectGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!IsGrabbable(obj))
            return false;

        if (TryAddItem(Helpers.SetForageStatsBasedOnProfession(Player, obj, tile)))
        {
            Location.Objects.Remove(tile);
            GainExperience(2, 7);
            return true;
        }
        return false;
    }

    private HashSet<int> GetBeachForageItems()
    {
        return new HashSet<int> { 393, 397, 152 };
    }

    private bool IsGrabbable(Object obj)
    {
        if (obj.bigCraftable.Value)
            return false;

        if (obj.isForage() && obj.ParentSheetIndex != 73)
            return true;

        if (Location is Beach && GetBeachForageItems().Contains(obj.ParentSheetIndex))
            return true;

        return ForageableItems.Contains(obj.ParentSheetIndex);
    }
}
