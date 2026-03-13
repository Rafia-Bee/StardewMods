#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RainyDayFishing.Interfaces;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Locations;

namespace RainyDayFishing;

/// <summary>
/// Provides a list of catchable fish for the To-Dew overlay on rainy days.
/// Dynamically reads from Data/Locations so modded fish are included automatically.
/// </summary>
public class RainyFishDataSource : IToDewOverlayDataSource
{
    private readonly ModEntry _mod;

    // Ignore time queries so the overlay shows all fish available at any point today,
    // not just fish available at the current time of day.
    private static readonly HashSet<string> IgnoreTimeKeys = new(StringComparer.OrdinalIgnoreCase) { "TIME" };

    public RainyFishDataSource(ModEntry mod)
    {
        _mod = mod;
    }

    public string GetSectionTitle()
    {
        return _mod.Helper.Translation.Get("overlay.section-title");
    }

    public List<(string text, bool isBold, Action? onDone)> GetItems(int limit)
    {
        if (!Context.IsWorldReady)
            return new();

        var fishMap = new Dictionary<string, SortedSet<string>>();
        bool anyRain = false;

        var visitedLocations = Game1.player.locationsVisited;
        Dictionary<string, string> rawFishData = DataLoader.Fish(Game1.content);

        foreach (string locName in visitedLocations)
        {
            var location = Game1.getLocationFromName(locName);
            if (location == null || !location.IsRainingHere())
                continue;

            anyRain = true;
            CollectFishForLocation(location, locName, rawFishData, fishMap);
        }

        if (!anyRain)
            return new();

        var results = new List<(string text, bool isBold, Action? onDone)>();
        foreach (var kvp in fishMap.OrderBy(x => x.Key))
        {
            string locations = string.Join(", ", kvp.Value);
            results.Add(($"{kvp.Key} — {locations}", false, null));
            if (results.Count >= limit)
                break;
        }

        return results;
    }

    private void CollectFishForLocation(
        GameLocation location,
        string locName,
        Dictionary<string, string> rawFishData,
        Dictionary<string, SortedSet<string>> fishMap)
    {
        Season seasonForLocation = Game1.GetSeasonForLocation(location);

        // Combine Default fish + location-specific fish, same as the game does
        IEnumerable<SpawnFishData> fishEntries = Enumerable.Empty<SpawnFishData>();
        if (Game1.locationData.TryGetValue("Default", out var defaultData) && defaultData.Fish != null)
            fishEntries = defaultData.Fish;

        var locData = location.GetData();
        if (locData?.Fish != null)
            fishEntries = fishEntries.Concat(locData.Fish);

        foreach (var spawn in fishEntries)
        {
            if (!IsCatchableNow(spawn, location, seasonForLocation, rawFishData))
                continue;

            if (!IsRainExclusive(spawn, rawFishData))
                continue;

            var itemData = ItemRegistry.GetData(spawn.ItemId);
            if (itemData == null)
                continue;

            string displayName = itemData.DisplayName;
            string locDisplayName = location.DisplayName ?? locName;

            if (!fishMap.ContainsKey(displayName))
                fishMap[displayName] = new SortedSet<string>();
            fishMap[displayName].Add(locDisplayName);
        }
    }

    /// <summary>
    /// Checks whether a fish requires rainy weather to spawn.
    /// Uses two sources: Data/Fish weather field (index 7) and SpawnFishData.Condition.
    /// </summary>
    private bool IsRainExclusive(SpawnFishData spawn, Dictionary<string, string> rawFishData)
    {
        if (!string.IsNullOrEmpty(spawn.Condition))
        {
            // Explicit rain requirement: WEATHER Here Rain, LOCATION_WEATHER Here Rain
            if (spawn.Condition.Contains("Rain", StringComparison.OrdinalIgnoreCase)
                && (spawn.Condition.Contains("WEATHER", StringComparison.OrdinalIgnoreCase)))
                return true;

            // Negated sun requirement: !WEATHER Here Sun (effectively = requires rain)
            if (spawn.Condition.Contains("!WEATHER", StringComparison.OrdinalIgnoreCase)
                && spawn.Condition.Contains("Sun", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check Data/Fish weather field (index 7 = "rainy" / "sunny" / "both")
        var itemData = ItemRegistry.GetData(spawn.ItemId);
        if (itemData != null && rawFishData.TryGetValue(itemData.ItemId, out string? fishStr))
        {
            var fields = fishStr.Split('/');
            if (fields.Length > 7 && fields[7] == "rainy")
                return true;
        }

        return false;
    }

    private bool IsCatchableNow(
        SpawnFishData spawn,
        GameLocation location,
        Season seasonForLocation,
        Dictionary<string, string> rawFishData)
    {
        // Season check
        if (spawn.Season.HasValue && spawn.Season.Value != seasonForLocation)
            return false;

        // Fishing level check
        if (Game1.player.FishingLevel < spawn.MinFishingLevel)
            return false;

        // Game state query condition — ignore TIME so we show all fish available
        // at any point today, not just right now
        if (!string.IsNullOrEmpty(spawn.Condition)
            && !GameStateQuery.CheckConditions(spawn.Condition, location, null, null, null, null, IgnoreTimeKeys))
            return false;

        // Skip trap (crab pot) fish
        var itemData = ItemRegistry.GetData(spawn.ItemId);
        if (itemData != null && rawFishData.TryGetValue(itemData.ItemId, out string? fishDataString))
        {
            var fields = fishDataString.Split('/');
            if (fields.Length > 1 && fields[1] == "trap")
                return false;
        }

        return true;
    }
}
