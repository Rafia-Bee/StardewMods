using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class GenericObjectGrabber : ObjectsMapGrabber
{
    public GenericObjectGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.forage || !Mod.IsForageGrabEnabled || !IsGrabbable(obj))
            return false;

        if (TryAddItem(Helpers.SetForageStatsBasedOnProfession(Player, obj, tile)))
        {
            Location.Objects.Remove(tile);
            GainExperience(2, 7);
            return true;
        }
        return false;
    }

    private bool IsGrabbable(Object obj)
    {
        if (obj.bigCraftable.Value)
            return false;

        return obj.isForage() && obj.ParentSheetIndex != 73;
    }
}
