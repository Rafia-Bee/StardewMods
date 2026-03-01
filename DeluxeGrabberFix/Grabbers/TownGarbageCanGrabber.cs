using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using StardewValley;
using StardewValley.GameData.GarbageCans;
using xTile;
using xTile.Layers;
using xTile.ObjectModel;
using xTile.Tiles;

namespace DeluxeGrabberFix.Grabbers;

internal class TownGarbageCanGrabber : MapGrabber
{
    public TownGarbageCanGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabItems()
    {
        if (!Config.garbageCans)
            return false;

        bool anyGrabbed = false;
        ReadOnlyCollection<Layer> layers = Location?.map?.Layers;
        if (layers == null)
            return false;

        foreach (Layer layer in layers)
        {
            Tile[,] tiles = layer.Tiles.Array;
            foreach (Tile tile in tiles)
            {
                if (tile == null)
                    continue;

                if (!tile.Properties.TryGetValue("Action", out PropertyValue actionValue))
                    continue;

                string action = actionValue.ToString();
                if (!action.StartsWith("Garbage"))
                    continue;

                Item item = null;
                string[] parts = ArgUtility.SplitBySpace(actionValue);
                string actionType = ArgUtility.Get(parts, 0, null, true);
                string canId = ArgUtility.Get(parts, 1, null, true);

                canId = canId switch
                {
                    "0" => "JodiAndKent",
                    "1" => "EmilyAndHaley",
                    "2" => "Mayor",
                    "3" => "Museum",
                    "4" => "Blacksmith",
                    "5" => "Saloon",
                    "6" => "Evelyn",
                    "7" => "JojaMart",
                    _ => canId
                };

                if (actionType == "Garbage" && canId != null
                    && Game1.netWorldState.Value.CheckedGarbage.Add(canId))
                {
                    GarbageCanItemData garbageData = new();
                    Random random = new();
                    Location.TryGetGarbageItem(canId, Player.DailyLuck, out item, out garbageData, out random, null);
                }

                anyGrabbed = TryAddItem(item) || anyGrabbed;
            }
        }
        return anyGrabbed;
    }
}
