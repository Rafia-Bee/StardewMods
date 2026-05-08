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
    private readonly HashSet<string> _cropsHarvestedLocations = new();
    private readonly HashSet<string> _dayCropsHarvestedLocations = new();
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

    internal void ReportCropsHarvested(GameLocation location)
    {
        string display = !string.IsNullOrEmpty(location.DisplayName) ? location.DisplayName : location.Name;
        _cropsHarvestedLocations.Add(display);
        _dayCropsHarvestedLocations.Add(display);
    }

    internal void ResetDayTracking()
    {
        _dayCropsHarvestedLocations.Clear();
    }

    internal void ShowEveningReplantReminder()
    {
        if (_dayCropsHarvestedLocations.Count > 0 && _mod.Config.replantReminder)
        {
            string locations = FormatList(_dayCropsHarvestedLocations);
            Game1.addHUDMessage(new HUDMessage(
                _mod.Helper.Translation.Get("hud.replant-reminder", new { locations })));
        }
    }

    internal void ResetGrabCycleTracking()
    {
        _namedGrabbersFull.Clear();
        _unnamedGrabbersFull.Clear();
        _activeGrabberNames.Clear();
        _cropsHarvestedLocations.Clear();
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
        if (_cropsHarvestedLocations.Count > 0 && _mod.Config.replantReminder)
        {
            string locations = FormatList(_cropsHarvestedLocations);
            Game1.addHUDMessage(new HUDMessage(
                _mod.Helper.Translation.Get("hud.replant-reminder", new { locations })));
            _cropsHarvestedLocations.Clear();
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

    /// Iterates every location and runs a full grab. Caller is responsible for
    /// holding an active <see cref="GrabSession"/> with <see cref="GrabSessionKind.ManualGlobalFire"/>;
    /// the session owns _isGrabbing, IsGlobalGrabActive, IsForageGrabEnabled, and the
    /// designated-grabbers cache.
    internal void FireGlobalGrab()
    {
        // Defense in depth (audit §2.5). Every keybind / deferred caller filters
        // config upstream, but a stale toggle in the gap (GMCM stays open across
        // ticks; auto-fire delays 1-5 ticks under Automate) could otherwise run a
        // global grab the player has just disabled. Skip here so future callers
        // can't make this mistake silently either.
        if (!ShouldFireGlobalGrab(_mod.Config, HasDesignatedGrabber()))
        {
            _mod.LogDebug("Skipping FireGlobalGrab: current config does not authorize a global fire");
            return;
        }

        _locations.DiscoverLocations();
        if (_mod.Config.Locations.selectVisitedOnly)
            _locations.ApplyVisitAutoSkip();

        _mod.LogDebug("Firing global grab");
        foreach (var location in ModEntry.GetAllLocations())
            GrabAtLocation(location);
    }

    /// Pure predicate for "is the current config valid for a global fire?"
    /// Pulled out so callers and tests can reason about it without owning a session.
    /// Authorized configs:
    ///   - Specialized + All / Hover  (Off blocks the keybind)
    ///   - Classic + All with at least one designated grabber
    ///   - Classic + Hover            (MapGrabber's Hover branch picks up the cursor target)
    /// Unauthorized: Off (any mode) and Classic + All with no designation.
    internal static bool ShouldFireGlobalGrab(ModConfig config, bool hasDesignatedGrabber)
    {
        if (config.globalGrabber == ModConfig.GlobalGrabberMode.Off)
            return false;

        if (config.grabberMode == ModConfig.GrabberMode.Classic
            && config.globalGrabber == ModConfig.GlobalGrabberMode.All
            && !hasDesignatedGrabber)
        {
            return false;
        }

        return true;
    }

    internal void HandleDesignateGrabber()
    {
        var cursorTile = Game1.lastCursorTile;
        var obj = Game1.player.currentLocation.getObjectAtTile((int)cursorTile.X, (int)cursorTile.Y);

        if (obj == null || !GrabberTypeHelper.IsGrabber(obj.QualifiedItemId)
            || obj.heldObject.Value is not StardewValley.Objects.Chest)
        {
            Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.hover-over-grabber"), HUDMessage.error_type));
            return;
        }

        ToggleGrabberDesignation(obj);
    }

    /// Single source of truth for the "make this grabber the global grabber" / "stop being the
    /// global grabber" toggle. Both the keybind path (HandleDesignateGrabber) and the in-menu
    /// button path (GlobalGrabberButton.TryClick) route through this helper so any future
    /// enhancement (confirm-before-clear, sound effects, audit logging, etc.) lands once for
    /// both entry points (audit §2.4). Returns the new designation state: true if this grabber
    /// is now the global grabber, false if it was just unset.
    internal bool ToggleGrabberDesignation(Object grabber)
    {
        if (grabber.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey))
        {
            grabber.modData.Remove(ModEntry.GlobalGrabberModDataKey);
            Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.no-longer-global")));
            return false;
        }

        ClearAllDesignations();
        grabber.modData[ModEntry.GlobalGrabberModDataKey] = "true";
        Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.now-global")));
        return true;
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

            var isSpecialized = _mod.Config.grabberMode == ModConfig.GrabberMode.Specialized;
            var beforeInventory = _mod.Config.reportYield ? aggregateGrabber.GetInventory() : null;
            var beforePerGrabber = _mod.Config.reportYield && isSpecialized
                ? aggregateGrabber.GetPerGrabberInventory() : null;
            bool result = aggregateGrabber.GrabItems();

            if (result)
                _mod.LogDebug($"Grab at {location.Name}: collected items");

            if (beforeInventory != null)
            {
                var afterInventory = aggregateGrabber.GetInventory();
                bool anyYield = false;

                if (isSpecialized && beforePerGrabber != null)
                {
                    var afterPerGrabber = aggregateGrabber.GetPerGrabberInventory();
                    var sb = new StringBuilder(_mod.Helper.Translation.Get("log.yield-header", new { location = location.Name }) + "\n");

                    foreach (var kvp in afterPerGrabber)
                    {
                        string grabberName = kvp.Key;
                        var afterItems = kvp.Value;
                        beforePerGrabber.TryGetValue(grabberName, out var beforeItems);
                        beforeItems ??= new Dictionary<InventoryEntry, int>();

                        bool grabberHasYield = false;
                        var itemLines = new StringBuilder();

                        foreach (var entry in afterItems)
                        {
                            int newCount = entry.Value;
                            if (beforeItems.ContainsKey(entry.Key))
                                newCount -= beforeItems[entry.Key];

                            if (newCount > 0)
                            {
                                itemLines.AppendLine(_mod.Helper.Translation.Get("log.yield-item", new
                                {
                                    name = entry.Key.DisplayName,
                                    quality = _mod.Helper.Translation.Get(entry.Key.QualityKey),
                                    count = newCount
                                }));
                                grabberHasYield = true;
                                _totalItemsGrabbed += newCount;
                            }
                        }

                        if (grabberHasYield)
                        {
                            sb.AppendLine($"  [{grabberName}]");
                            sb.Append(itemLines);
                            anyYield = true;
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
                else
                {
                    var grabberNames = aggregateGrabber.GrabberObjects
                        .Select(g => ModEntry.GetGrabberDisplayName(g))
                        .Distinct()
                        .ToList();
                    string header = grabberNames.Any(n => ModEntry.GetGrabberCustomName(
                            aggregateGrabber.GrabberObjects.First(g => ModEntry.GetGrabberDisplayName(g) == n)) != null)
                        ? _mod.Helper.Translation.Get("log.yield-header-named", new { names = string.Join(", ", grabberNames) })
                        : _mod.Helper.Translation.Get("log.yield-header", new { location = location.Name });
                    var sb = new StringBuilder(header + "\n");

                    foreach (var entry in afterInventory)
                    {
                        int newCount = entry.Value;
                        if (beforeInventory.ContainsKey(entry.Key))
                            newCount -= beforeInventory[entry.Key];

                        if (newCount > 0)
                        {
                            sb.AppendLine(_mod.Helper.Translation.Get("log.yield-item", new
                            {
                                name = entry.Key.DisplayName,
                                quality = _mod.Helper.Translation.Get(entry.Key.QualityKey),
                                count = newCount
                            }));
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

    internal bool GrabMachinesAtLocation(GameLocation location)
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
            var machineGrabber = new MachineGrabber(_mod, location) { BelongsToType = GrabberType.Machine };
            if (!machineGrabber.CanGrab())
                return false;

            machineGrabber.CleanupGrabberChests();

            var beforeInventory = _mod.Config.reportYield ? machineGrabber.GetInventory() : null;

            bool result = machineGrabber.GrabItems();

            if (result)
                _mod.LogDebug($"Machine grab at {location.Name}: collected items");

            if (beforeInventory != null && result)
            {
                var afterInventory = machineGrabber.GetInventory();
                var sb = new StringBuilder(_mod.Helper.Translation.Get("log.machine-yield-header", new { location = location.Name }) + "\n");
                bool anyYield = false;

                foreach (var entry in afterInventory)
                {
                    int newCount = entry.Value;
                    if (beforeInventory.ContainsKey(entry.Key))
                        newCount -= beforeInventory[entry.Key];

                    if (newCount > 0)
                    {
                        sb.AppendLine(_mod.Helper.Translation.Get("log.yield-item", new
                        {
                            name = entry.Key.DisplayName,
                            quality = _mod.Helper.Translation.Get(entry.Key.QualityKey),
                            count = newCount
                        }));
                        anyYield = true;
                        _totalItemsGrabbed += newCount;
                    }
                }

                if (anyYield)
                    _mod.Monitor.Log(sb.ToString(), LogLevel.Info);
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

    internal bool GrabForageAtLocation(GameLocation location)
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
            var objectGrabber = new GenericObjectGrabber(_mod, location) { BelongsToType = GrabberType.Forage };
            if (!objectGrabber.CanGrab())
                return false;

            objectGrabber.CleanupGrabberChests();

            var beforeInventory = _mod.Config.reportYield ? objectGrabber.GetInventory() : null;

            bool result = objectGrabber.GrabItems();

            var featureGrabber = new ForageHoeDirtGrabber(_mod, location) { BelongsToType = GrabberType.Forage };
            result |= featureGrabber.GrabItems();

            if (result)
                _mod.LogDebug($"Forage grab at {location.Name}: collected items");

            if (beforeInventory != null && result)
            {
                var afterInventory = objectGrabber.GetInventory();
                var sb = new StringBuilder(_mod.Helper.Translation.Get("log.forage-yield-header", new { location = location.Name }) + "\n");
                bool anyYield = false;

                foreach (var entry in afterInventory)
                {
                    int newCount = entry.Value;
                    if (beforeInventory.ContainsKey(entry.Key))
                        newCount -= beforeInventory[entry.Key];

                    if (newCount > 0)
                    {
                        sb.AppendLine(_mod.Helper.Translation.Get("log.yield-item", new
                        {
                            name = entry.Key.DisplayName,
                            quality = _mod.Helper.Translation.Get(entry.Key.QualityKey),
                            count = newCount
                        }));
                        anyYield = true;
                        _totalItemsGrabbed += newCount;
                    }
                }

                if (anyYield)
                    _mod.Monitor.Log(sb.ToString(), LogLevel.Info);
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
