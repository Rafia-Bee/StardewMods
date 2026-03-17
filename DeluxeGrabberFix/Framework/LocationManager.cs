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

    public LocationManager(ModEntry mod)
    {
        _mod = mod;
    }

    internal void LoadSaveData()
    {
        SaveData = _mod.Helper.Data.ReadSaveData<SaveData>(SaveDataKey) ?? new SaveData();
    }

    internal void ClearState()
    {
        DiscoveredLocations = null;
        SaveData = null;
    }

    internal void WriteSaveData()
    {
        if (SaveData != null)
            _mod.Helper.Data.WriteSaveData(SaveDataKey, SaveData);
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
        if (!_mod.Config.selectVisitedOnly || DiscoveredLocations == null)
        {
            _mod.Monitor.Log($"ApplyVisitAutoSkip skipped: selectVisitedOnly={_mod.Config.selectVisitedOnly}, discoveredLocations={DiscoveredLocations?.Count ?? -1}", LogLevel.Info);
            return;
        }

        if (SaveData == null)
        {
            _mod.Monitor.Log("ApplyVisitAutoSkip skipped: SaveData is null", LogLevel.Info);
            return;
        }

        _mod.Config.SkippedLocations ??= new HashSet<string>();
        int skipped = 0;
        int enabled = 0;

        foreach (var (locName, _) in DiscoveredLocations)
        {
            bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);

            if (!visited
                && !_mod.Config.SkippedLocations.Contains(locName)
                && !SaveData.AutoSkippedLocations.Contains(locName)
                && !SaveData.ManuallyManagedLocations.Contains(locName))
            {
                _mod.Config.SkippedLocations.Add(locName);
                SaveData.AutoSkippedLocations.Add(locName);
                skipped++;
            }
            else if (visited && SaveData.AutoSkippedLocations.Contains(locName))
            {
                _mod.Config.SkippedLocations.Remove(locName);
                SaveData.AutoSkippedLocations.Remove(locName);
                enabled++;
            }
        }

        _mod.Monitor.Log($"ApplyVisitAutoSkip: {DiscoveredLocations.Count} locations checked, {skipped} auto-skipped, {enabled} auto-enabled", LogLevel.Info);

        if (skipped > 0 || enabled > 0)
        {
            _mod.Helper.WriteConfig(_mod.Config);
            WriteSaveData();
        }
    }

    internal bool ShouldProcessLocation(GameLocation location)
    {
        if (location == null)
            return false;

        string name = location.Name;
        if (string.IsNullOrEmpty(name))
            return false;

        if (_mod.Config.SkippedLocations?.Contains(name) == true)
        {
            _mod.LogDebug($"Skipping {name}: disabled in config");
            return false;
        }

        if (_mod.Config.skipFestivalLocations)
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
        if (!_mod.Config.selectVisitedOnly || SaveData == null)
            return false;

        if (string.IsNullOrEmpty(locationName))
            return false;

        bool wasSkipped = _mod.Config.SkippedLocations?.Remove(locationName) == true;
        SaveData.AutoSkippedLocations.Remove(locationName);

        if (wasSkipped)
        {
            SaveData.ManuallyManagedLocations.Add(locationName);
            _mod.Helper.WriteConfig(_mod.Config);
            WriteSaveData();
            _mod.LogDebug($"Auto-enabled location after visit: {locationName}");
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
