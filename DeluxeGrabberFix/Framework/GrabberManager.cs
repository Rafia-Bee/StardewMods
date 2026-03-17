using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeluxeGrabberFix.Grabbers;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Framework;

internal class GrabberManager
{
    private readonly ModEntry _mod;
    private readonly LocationManager _locations;

    private readonly HashSet<string> _namedGrabbersFull = new();
    private readonly HashSet<string> _unnamedGrabbersFull = new();
    private readonly HashSet<string> _activeGrabberNames = new();
    private int _totalItemsGrabbed;

    public GrabberManager(ModEntry mod, LocationManager locations)
    {
        _mod = mod;
        _locations = locations;
    }

    internal void ReportChestFull(Object grabber)
    {
        string customName = ModEntry.GetGrabberCustomName(grabber);
        if (customName != null)
            _namedGrabbersFull.Add(customName);
        else
        {
            var loc = grabber.Location;
            string display = loc != null
                ? (!string.IsNullOrEmpty(loc.DisplayName) ? loc.DisplayName : loc.Name)
                : "Auto-Grabber";
            _unnamedGrabbersFull.Add(display);
        }
    }

    internal void ResetGrabCycleTracking()
    {
        _namedGrabbersFull.Clear();
        _unnamedGrabbersFull.Clear();
        _activeGrabberNames.Clear();
        _totalItemsGrabbed = 0;
    }

    internal void ShowGrabCycleResults(bool showSummary)
    {
        if (_namedGrabbersFull.Count > 0)
        {
            string names = FormatList(_namedGrabbersFull);
            Game1.addHUDMessage(new HUDMessage(
                _mod.Helper.Translation.Get("hud.named-grabber-full", new { names }),
                HUDMessage.error_type));
            _namedGrabbersFull.Clear();
        }

        if (_unnamedGrabbersFull.Count > 0)
        {
            string locations = FormatList(_unnamedGrabbersFull);
            Game1.addHUDMessage(new HUDMessage(
                _mod.Helper.Translation.Get("hud.grabber-full", new { locations }),
                HUDMessage.error_type));
            _unnamedGrabbersFull.Clear();
        }

        if (showSummary && _totalItemsGrabbed > 0 && _mod.Config.reportYield)
        {
            if (_activeGrabberNames.Count > 0)
            {
                string names = FormatList(_activeGrabberNames);
                Game1.addHUDMessage(new HUDMessage(
                    _mod.Helper.Translation.Get("hud.named-grab-summary", new { names, count = _totalItemsGrabbed })));
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(
                    _mod.Helper.Translation.Get("hud.grab-summary", new { count = _totalItemsGrabbed })));
            }
        }
        _activeGrabberNames.Clear();
        _totalItemsGrabbed = 0;
    }

    private string FormatList(HashSet<string> items)
    {
        const int maxShown = 3;
        var list = items.ToList();
        if (list.Count <= maxShown)
            return string.Join(", ", list);
        return string.Join(", ", list.Take(maxShown))
               + _mod.Helper.Translation.Get("hud.grabber-full-overflow", new { count = list.Count - maxShown });
    }

    internal void FireGlobalGrab()
    {
        _locations.DiscoverLocations();
        if (_mod.Config.selectVisitedOnly)
            _locations.ApplyVisitAutoSkip();

        _mod.LogDebug("Firing global grab");
        _mod.IsGlobalGrabActive = true;
        _mod.IsForageGrabEnabled = true;
        try
        {
            var allLocations = ModEntry.GetAllLocations().ToList();

            if (_mod.Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
            {
                _mod.CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
                foreach (var loc in allLocations)
                {
                    _mod.CachedDesignatedGrabbers.AddRange(
                        loc.Objects.Pairs
                            .Where(pair => pair.Value != null
                                && pair.Value.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey))
                            .ToList());
                }
            }

            foreach (var location in allLocations)
            {
                GrabAtLocation(location);
            }
        }
        finally
        {
            _mod.IsGlobalGrabActive = false;
            _mod.IsForageGrabEnabled = false;
            _mod.CachedDesignatedGrabbers = null;
        }
    }

    internal void HandleDesignateGrabber()
    {
        var cursorTile = Game1.lastCursorTile;
        var obj = Game1.player.currentLocation.getObjectAtTile((int)cursorTile.X, (int)cursorTile.Y);

        if (obj == null || obj.QualifiedItemId != BigCraftableIds.AutoGrabber
            || obj.heldObject.Value is not StardewValley.Objects.Chest)
        {
            Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.hover-over-grabber"), HUDMessage.error_type));
            return;
        }

        if (obj.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey))
        {
            obj.modData.Remove(ModEntry.GlobalGrabberModDataKey);
            Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.no-longer-global")));
            return;
        }

        ClearAllDesignations();
        obj.modData[ModEntry.GlobalGrabberModDataKey] = "true";
        Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.now-global")));
    }

    internal void ClearAllDesignations()
    {
        foreach (var location in ModEntry.GetAllLocations())
        {
            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey))
                    pair.Value.modData.Remove(ModEntry.GlobalGrabberModDataKey);
            }
        }
    }

    internal bool HasDesignatedGrabber()
    {
        foreach (var location in ModEntry.GetAllLocations())
        {
            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey))
                    return true;
            }
        }
        return false;
    }

    internal bool GrabAtLocation(GameLocation location)
    {
        if (!_locations.ShouldProcessLocation(location))
            return false;

        _mod.UseLocationCache = true;
        _mod.CachedGrabberPairs = null;
        _mod.CachedObjectPairs = null;
        _mod.CachedFeaturePairs = null;
        _mod.GrabbedTiles = new HashSet<Vector2>();

        try
        {
            var aggregateGrabber = new AggregateDailyGrabber(_mod, location);

            if (!aggregateGrabber.CanGrab())
            {
                _mod.LogDebug($"No valid auto-grabbers at {location.Name}, skipping");
                return false;
            }

            aggregateGrabber.CleanupGrabberChests();

            var beforeInventory = _mod.Config.reportYield ? aggregateGrabber.GetInventory() : null;
            bool result = aggregateGrabber.GrabItems();

            _mod.LogDebug($"Grab at {location.Name}: {(result ? "collected items" : "nothing to collect")}");

            if (beforeInventory != null)
            {
                var afterInventory = aggregateGrabber.GetInventory();
                var grabberNames = aggregateGrabber.GrabberObjects
                    .Select(g => ModEntry.GetGrabberDisplayName(g))
                    .Distinct()
                    .ToList();
                string header = grabberNames.Any(n => ModEntry.GetGrabberCustomName(
                        aggregateGrabber.GrabberObjects.First(g => ModEntry.GetGrabberDisplayName(g) == n)) != null)
                    ? $"Yield of {string.Join(", ", grabberNames)}:"
                    : $"Yield of autograbber(s) at {location.Name}:";
                var sb = new StringBuilder(header + "\n");
                bool anyYield = false;

                foreach (var entry in afterInventory)
                {
                    int newCount = entry.Value;
                    if (beforeInventory.ContainsKey(entry.Key))
                        newCount -= beforeInventory[entry.Key];

                    if (newCount > 0)
                    {
                        sb.AppendLine($"    {entry.Key.Name} ({entry.Key.QualityName}) x{newCount}");
                        anyYield = true;
                        _totalItemsGrabbed += newCount;
                    }
                }

                if (anyYield)
                {
                    foreach (var g in aggregateGrabber.GrabberObjects)
                    {
                        var customName = ModEntry.GetGrabberCustomName(g);
                        if (customName != null)
                            _activeGrabberNames.Add(customName);
                    }
                    _mod.Monitor.Log(sb.ToString(), LogLevel.Info);
                }
            }

            return result;
        }
        finally
        {
            _mod.UseLocationCache = false;
            _mod.CachedGrabberPairs = null;
            _mod.CachedObjectPairs = null;
            _mod.CachedFeaturePairs = null;
            _mod.GrabbedTiles = null;
        }
    }
}
