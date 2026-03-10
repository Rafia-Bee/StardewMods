using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.Objects;

namespace DeluxeGrabberFix.Grabbers;

internal class MachineGrabber : ObjectsMapGrabber
{
    public MachineGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.collectMachines)
            return false;

        if (!obj.readyForHarvest.Value || obj.heldObject.Value == null)
            return false;

        if (IsCrabPot(obj))
            return GrabCrabPot(tile, obj);

        if (IsBeeHouse(obj))
            return GrabBeeHouse(tile, obj);

        if (IsTapper(obj))
            return GrabTapper(tile, obj);

        return false;
    }

    private bool GrabCrabPot(Vector2 tile, Object obj)
    {
        if (!Config.collectCrabPots)
            return false;

        var output = obj.heldObject.Value;
        if (TryAddItem(output))
        {
            Mod.LogDebug($"Collected {output.Name} x{output.Stack} from crab pot at {Location.Name} [{tile}]");
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;

            if (obj is CrabPot crabPot)
                crabPot.bait.Value = null;

            GainExperience(1, 5);
            return true;
        }
        return false;
    }

    private bool GrabBeeHouse(Vector2 tile, Object obj)
    {
        if (!Config.collectBeeHouses)
            return false;

        var output = obj.heldObject.Value;
        if (TryAddItem(output))
        {
            Mod.LogDebug($"Collected {output.Name} x{output.Stack} from bee house at {Location.Name} [{tile}]");
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;
            return true;
        }
        return false;
    }

    private bool GrabTapper(Vector2 tile, Object obj)
    {
        if (!Config.collectTappers)
            return false;

        var output = obj.heldObject.Value;
        if (TryAddItem(output))
        {
            Mod.LogDebug($"Collected {output.Name} x{output.Stack} from tapper at {Location.Name} [{tile}]");
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;
            return true;
        }
        return false;
    }

    private static bool IsCrabPot(Object obj)
        => obj is CrabPot;

    private static bool IsBeeHouse(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.BeeHouse;

    private static bool IsTapper(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.Tapper || obj.QualifiedItemId == BigCraftableIds.HeavyTapper;
}
