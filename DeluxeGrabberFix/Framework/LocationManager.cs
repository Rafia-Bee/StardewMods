using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal class LocationManager
{
    private readonly ModEntry _mod;
    private const string SaveDataKey = "visit-tracking";

    internal List<(string Name, string DisplayName)> DiscoveredLocations { get; private set; }
    internal SaveData SaveData { get; private set; }

    // Audit §3.11: per-warp `HandleLocationVisit` un-skips would WriteSaveData +
    // SaveActiveConfig immediately, causing one config-write + one savedata-write
    // per visit. A player walking through 20 locations in 5 minutes paid 40 disk
    // writes. These flags coalesce mark-dirty calls into one flush at known safe
    // boundaries (OnDayEnding, OnReturnedToTitle); the GMCM Save and OnSaveLoaded
    // paths keep their immediate-write behavior because those are user-explicit
    // (or already once-per-session) and need synchronous durability.
    private bool _saveDataDirty;
    private bool _configDirty;

    public LocationManager(ModEntry mod)
    {
        _mod = mod;
    }

    internal void LoadSaveData()
    {
        SaveData = _mod.Helper.Data.ReadSaveData<SaveData>(SaveDataKey) ?? new SaveData();
        // Fresh load: anything we'd flush is whatever's already on disk.
        _saveDataDirty = false;
        _configDirty = false;
    }

    internal void ClearState()
    {
        DiscoveredLocations = null;
        SaveData = null;
        _saveDataDirty = false;
        _configDirty = false;
    }

    internal void WriteSaveData()
    {
        if (SaveData != null)
        {
            _mod.Helper.Data.WriteSaveData(SaveDataKey, SaveData);
            _saveDataDirty = false;
        }
    }

    // Mark-dirty path for per-warp / per-visit mutation. Pairs with FlushDirtyState
    // at the OnDayEnding / OnReturnedToTitle boundaries so we batch many small
    // mutations into one disk write.
    internal void MarkPersistentStateDirty()
    {
        _saveDataDirty = true;
        _configDirty = true;
    }

    internal void FlushDirtyState()
    {
        if (_configDirty)
        {
            _mod.ConfigManager.SaveActiveConfig();
            _configDirty = false;
        }
        if (_saveDataDirty)
        {
            WriteSaveData();
        }
    }

    internal void DiscoverLocations()
    {
        DiscoveredLocations = ModEntry.GetAllLocations()
            .Where(loc => !string.IsNullOrEmpty(loc.Name))
            .GroupBy(loc => loc.Name)
            .Select(g => (Name: g.Key, DisplayName: GetLocationDisplayName(g.First())))
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    internal void ApplyVisitAutoSkip()
    {
        if (!_mod.Config.Locations.selectVisitedOnly || DiscoveredLocations == null)
        {
            _mod.LogDebug($"ApplyVisitAutoSkip skipped: selectVisitedOnly={_mod.Config.Locations.selectVisitedOnly}, discoveredLocations={DiscoveredLocations?.Count ?? -1}");
            return;
        }

        if (SaveData == null)
        {
            _mod.LogDebug("ApplyVisitAutoSkip skipped: SaveData is null");
            return;
        }

        _mod.Config.Locations.SkippedLocations ??= new HashSet<string>();
        int skipped = 0;
        int enabled = 0;

        foreach (var (locName, _) in DiscoveredLocations)
        {
            bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);

            if (!visited
                && !_mod.Config.Locations.SkippedLocations.Contains(locName)
                && !SaveData.AutoSkippedLocations.Contains(locName)
                && !SaveData.ManuallyManagedLocations.Contains(locName))
            {
                _mod.Config.Locations.SkippedLocations.Add(locName);
                SaveData.AutoSkippedLocations.Add(locName);
                skipped++;
            }
            else if (visited && SaveData.AutoSkippedLocations.Contains(locName))
            {
                _mod.Config.Locations.SkippedLocations.Remove(locName);
                SaveData.AutoSkippedLocations.Remove(locName);
                enabled++;
            }
        }

        _mod.LogDebug($"ApplyVisitAutoSkip: {DiscoveredLocations.Count} locations checked, {skipped} auto-skipped, {enabled} auto-enabled");

        if (skipped > 0 || enabled > 0)
        {
            // Audit §3.11: ApplyVisitAutoSkip is called once per save load and
            // once per GMCM save (when selectVisitedOnly is on). Both boundaries
            // expect synchronous persistence -- flush immediately so the Enabled
            // Locations page in GMCM and any subsequent ShouldProcessLocation
            // checks see the freshly-written state on disk.
            _mod.ConfigManager.SaveActiveConfig();
            WriteSaveData();
            _configDirty = false;
        }
    }

    internal bool ShouldProcessLocation(GameLocation location)
    {
        if (location == null)
            return false;

        string name = location.Name;
        if (string.IsNullOrEmpty(name))
            return false;

        if (_mod.Config.Locations.SkippedLocations?.Contains(name) == true)
        {
            _mod.LogDebug($"Skipping {name}: disabled in config");
            return false;
        }

        if (_mod.Config.Locations.skipFestivalLocations)
        {
            if (name.Contains("Festival", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Temp", StringComparison.OrdinalIgnoreCase))
            {
                _mod.LogDebug($"Skipping {name}: festival/event location");
                return false;
            }

            if (Game1.isFestival())
            {
                _mod.LogDebug($"Skipping {name}: festival currently active");
                return false;
            }
        }

        return true;
    }

    internal bool HandleLocationVisit(string locationName)
    {
        if (!_mod.Config.Locations.selectVisitedOnly || SaveData == null)
            return false;

        if (string.IsNullOrEmpty(locationName))
            return false;

        if (SaveData.BlacklistedLocations.Contains(locationName))
            return false;

        bool wasSkipped = _mod.Config.Locations.SkippedLocations?.Remove(locationName) == true;
        SaveData.AutoSkippedLocations.Remove(locationName);

        if (wasSkipped)
        {
            // Audit §3.11: per-warp un-skip just flips the dirty flag. The actual
            // disk write happens at OnDayEnding / OnReturnedToTitle via FlushDirtyState.
            // Risk: a force-quit between visits and the next flush boundary loses
            // the un-skip; the player re-walks on next session, no permanent damage.
            MarkPersistentStateDirty();
            _mod.LogDebug($"Auto-enabled location after visit: {locationName} (dirty flag set, deferred flush)");
            return true;
        }
        return false;
    }

    internal static string GetLocationDisplayName(GameLocation location)
    {
        string display = location.DisplayName;

        if (string.IsNullOrEmpty(display)
            || display.StartsWith("(no translation", StringComparison.OrdinalIgnoreCase))
        {
            display = location.Name;

            if (display.StartsWith("Custom_"))
                display = display.Substring(7);

            display = display.Replace('_', ' ');
        }

        return display;
    }
}
