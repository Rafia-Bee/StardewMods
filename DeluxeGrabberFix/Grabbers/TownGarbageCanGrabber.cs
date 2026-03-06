using System.Collections.Generic;
using StardewValley;
using xTile.Layers;
using xTile.ObjectModel;
using xTile.Tiles;

namespace DeluxeGrabberFix.Grabbers;

internal class TownGarbageCanGrabber : MapGrabber
{
    private static readonly Dictionary<string, List<string>> CachedCanIds = new();

    public TownGarbageCanGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public static void ClearCache() => CachedCanIds.Clear();

    public override bool GrabItems()
    {
        if (!Config.garbageCans)
            return false;

        List<string> canIds = GetGarbageCanIds();
        if (canIds.Count == 0)
            return false;

        bool anyGrabbed = false;

        foreach (string canId in canIds)
        {
            if (!Game1.netWorldState.Value.CheckedGarbage.Add(canId))
                continue;

            Location.TryGetGarbageItem(canId, Player.DailyLuck, out Item item, out _, out _, null);
            anyGrabbed = TryAddItem(item) || anyGrabbed;
        }

        return anyGrabbed;
    }

    private List<string> GetGarbageCanIds()
    {
        string locationName = Location.NameOrUniqueName;
        if (CachedCanIds.TryGetValue(locationName, out var cached))
            return cached;

        var canIds = new List<string>();
        Layer buildingsLayer = Location.map?.GetLayer("Buildings");
        if (buildingsLayer == null)
        {
            CachedCanIds[locationName] = canIds;
            return canIds;
        }

        Tile[,] tiles = buildingsLayer.Tiles.Array;
        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                Tile tile = tiles[x, y];
                if (tile == null)
                    continue;

                if (!tile.Properties.TryGetValue("Action", out PropertyValue actionValue))
                    continue;

                string[] parts = ArgUtility.SplitBySpace(actionValue);
                string actionType = ArgUtility.Get(parts, 0, null, true);
                if (actionType != "Garbage")
                    continue;

                string canId = ArgUtility.Get(parts, 1, null, true);
                if (canId == null)
                    continue;

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

                canIds.Add(canId);
            }
        }

        CachedCanIds[locationName] = canIds;
        return canIds;
    }
}
