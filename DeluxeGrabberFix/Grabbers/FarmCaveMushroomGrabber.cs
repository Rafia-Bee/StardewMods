using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Internal;

namespace DeluxeGrabberFix.Grabbers;

internal class FarmCaveMushroomGrabber : ObjectsMapGrabber
{
    private readonly Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetMushroomHarvest;
    private readonly HashSet<Vector2> _automateSkipTiles;

    public FarmCaveMushroomGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        GetMushroomHarvest = Mod.Api.GetMushroomHarvest ?? DefaultGetMushroomHarvest;
        _automateSkipTiles = AutomateSkipTiles.Get(mod, location);
    }

    private KeyValuePair<Object, int> DefaultGetMushroomHarvest(Object mushroom, Vector2 mushroomBoxTile, GameLocation location)
    {
        mushroom.Quality = 0;
        return new KeyValuePair<Object, int>(mushroom, 0);
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (Config.disableMachineCollection || !Config.farmCaveMushrooms || obj.QualifiedItemId != BigCraftableIds.MushroomBox)
            return false;

        if (!obj.readyForHarvest.Value || obj.heldObject.Value == null)
            return false;

        if (_automateSkipTiles != null && _automateSkipTiles.Contains(tile))
        {
            Mod.LogDebug($"Skipping mushroom box at {Location.Name} [{tile}] (managed by Automate)");
            return false;
        }

        var harvest = GetMushroomHarvest(obj.heldObject.Value, tile, Location);
        if (TryAddItem(harvest.Key))
        {
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;
            obj.ResetParentSheetIndex();

            var machineData = obj.GetMachineData();
            if (machineData != null
                && MachineDataUtility.TryGetMachineOutputRule(obj, machineData, MachineOutputTrigger.OutputCollected, null, Player, Location, out var rule, out _, out _, out _))
            {
                obj.OutputMachine(machineData, rule, obj.lastInputItem?.Value, Player, Location, probe: false);
            }

            GainExperience(2, harvest.Value);
            return true;
        }
        return false;
    }
}
