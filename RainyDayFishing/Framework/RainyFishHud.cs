#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.Menus;

namespace RainyDayFishing.Framework;

/// <summary>
/// Renders rain-exclusive fish icons on the right side of the HUD.
/// Hovering over an icon shows spawn locations and catchable time windows.
/// </summary>
public sealed class RainyFishHud
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly Func<ModConfig> _getConfig;

    private List<FishEntry> _entries = new();
    private bool _isRainy;

    private static readonly HashSet<string> IgnoreTimeKeys =
        new(StringComparer.OrdinalIgnoreCase) { "TIME" };

    private const int IconSize = 40;
    private const int IconPadding = 5;

    public RainyFishHud(IModHelper helper, IMonitor monitor, Func<ModConfig> getConfig)
    {
        _helper = helper;
        _monitor = monitor;
        _getConfig = getConfig;
    }

    public void Clear()
    {
        _entries.Clear();
        _isRainy = false;
    }

    public void Refresh()
    {
        _entries.Clear();
        _isRainy = false;

        if (!Context.IsWorldReady)
            return;

        var fishMap = new Dictionary<string, FishEntry>();
        var rawFishData = DataLoader.Fish(Game1.content);

        foreach (string locName in Game1.player.locationsVisited)
        {
            var location = Game1.getLocationFromName(locName);
            if (location == null || !location.IsRainingHere())
                continue;

            _isRainy = true;
            CollectFish(location, locName, rawFishData, fishMap);
        }

        _entries = fishMap.Values.OrderBy(f => f.DisplayName).ToList();
    }

    public void Draw(SpriteBatch b)
    {
        if (!_isRainy || _entries.Count == 0 || !Context.IsWorldReady)
            return;
        if (Game1.eventUp || Game1.currentMinigame != null || Game1.activeClickableMenu != null)
            return;
        if (!_getConfig().Enabled)
            return;

        var config = _getConfig();
        int baseX = config.HudX < 0
            ? Game1.uiViewport.Width + config.HudX
            : config.HudX;
        int baseY = config.HudY;
        bool horizontal = config.HorizontalLayout;

        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        FishEntry? hovered = null;

        for (int i = 0; i < _entries.Count; i++)
        {
            var fish = _entries[i];
            int x = horizontal ? baseX + i * (IconSize + IconPadding) : baseX;
            int y = horizontal ? baseY : baseY + i * (IconSize + IconPadding);
            var dest = new Rectangle(x, y, IconSize, IconSize);

            bool isHover = dest.Contains(mouseX, mouseY);
            if (isHover)
                hovered = fish;

            b.Draw(Game1.staminaRect, dest, Color.Black * 0.25f);

            if (isHover)
                dest.Inflate(3, 3);

            b.Draw(fish.Texture, dest, fish.SourceRect, Color.White);
        }

        if (hovered != null)
            DrawTooltip(b, hovered);
    }

    private void DrawTooltip(SpriteBatch b, FishEntry fish)
    {
        var sb = new StringBuilder();
        if (!Game1.player.fishCaught.ContainsKey(fish.QualifiedItemId))
            sb.AppendLine("(Uncaught)");

        int maxLocs = _getConfig().MaxLocations;
        var locations = fish.LocationTimes;
        int shown = maxLocs > 0 ? Math.Min(locations.Count, maxLocs) : locations.Count;

        for (int i = 0; i < shown; i++)
        {
            var info = locations[i];
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(info.Location);
            if (!string.IsNullOrEmpty(info.TimeRange))
                sb.Append($"  ({info.TimeRange})");
        }

        int remaining = locations.Count - shown;
        if (remaining > 0)
        {
            sb.AppendLine();
            sb.Append($"...and {remaining} more");
        }

        IClickableMenu.drawHoverText(
            b,
            sb.ToString(),
            Game1.smallFont,
            boldTitleText: fish.DisplayName
        );
    }

    private void CollectFish(
        GameLocation location,
        string locName,
        Dictionary<string, string> rawFishData,
        Dictionary<string, FishEntry> fishMap)
    {
        Season season = Game1.GetSeasonForLocation(location);

        IEnumerable<SpawnFishData> spawnEntries = Enumerable.Empty<SpawnFishData>();
        if (Game1.locationData.TryGetValue("Default", out var defaultData) && defaultData.Fish != null)
            spawnEntries = defaultData.Fish;

        var locData = location.GetData();
        if (locData?.Fish != null)
            spawnEntries = spawnEntries.Concat(locData.Fish);

        foreach (var spawn in spawnEntries)
        {
            if (!IsCatchableToday(spawn, location, season, rawFishData))
                continue;
            if (!IsRainExclusive(spawn, rawFishData))
                continue;

            var itemData = ItemRegistry.GetData(spawn.ItemId);
            if (itemData == null)
                continue;

            string displayName = itemData.DisplayName;
            string locDisplayName = location.DisplayName ?? locName;
            string timeRange = GetTimeRange(itemData.ItemId, rawFishData);

            if (!fishMap.TryGetValue(spawn.ItemId, out var entry))
            {
                entry = new FishEntry(
                    spawn.ItemId,
                    itemData.QualifiedItemId,
                    displayName,
                    itemData.GetTexture(),
                    itemData.GetSourceRect()
                );
                fishMap[spawn.ItemId] = entry;
            }

            if (!entry.LocationTimes.Any(lt => lt.Location == locDisplayName))
                entry.LocationTimes.Add(new LocationTime(locDisplayName, timeRange));
        }
    }

    private static string GetTimeRange(string itemId, Dictionary<string, string> rawFishData)
    {
        if (!rawFishData.TryGetValue(itemId, out string? fishStr))
            return "";

        var fields = fishStr.Split('/');
        if (fields.Length <= 5)
            return "";

        var parts = fields[5].Split(' ');
        if (parts.Length < 2 || parts.Length % 2 != 0)
            return "";

        var ranges = new List<string>();
        for (int i = 0; i < parts.Length; i += 2)
        {
            if (int.TryParse(parts[i], out int start) && int.TryParse(parts[i + 1], out int end))
                ranges.Add($"{FormatTime(start)} – {FormatTime(end)}");
        }

        return string.Join(", ", ranges);
    }

    private static string FormatTime(int time)
    {
        int hours = time / 100;
        int displayHours = hours % 12;
        if (displayHours == 0)
            displayHours = 12;
        string period = (hours % 24) >= 12 ? "pm" : "am";
        int minutes = time % 100;
        return minutes == 0
            ? $"{displayHours}{period}"
            : $"{displayHours}:{minutes:D2}{period}";
    }

    private static bool IsRainExclusive(SpawnFishData spawn, Dictionary<string, string> rawFishData)
    {
        if (!string.IsNullOrEmpty(spawn.Condition))
        {
            if (spawn.Condition.Contains("Rain", StringComparison.OrdinalIgnoreCase)
                && spawn.Condition.Contains("WEATHER", StringComparison.OrdinalIgnoreCase))
                return true;
            if (spawn.Condition.Contains("!WEATHER", StringComparison.OrdinalIgnoreCase)
                && spawn.Condition.Contains("Sun", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var itemData = ItemRegistry.GetData(spawn.ItemId);
        if (itemData != null && rawFishData.TryGetValue(itemData.ItemId, out string? fishStr))
        {
            var fields = fishStr.Split('/');
            if (fields.Length > 7 && fields[7] == "rainy")
                return true;
        }

        return false;
    }

    private static bool IsCatchableToday(
        SpawnFishData spawn,
        GameLocation location,
        Season season,
        Dictionary<string, string> rawFishData)
    {
        if (spawn.Season.HasValue && spawn.Season.Value != season)
            return false;
        if (Game1.player.FishingLevel < spawn.MinFishingLevel)
            return false;
        if (!string.IsNullOrEmpty(spawn.Condition)
            && !GameStateQuery.CheckConditions(spawn.Condition, location, null, null, null, null, IgnoreTimeKeys))
            return false;

        var itemData = ItemRegistry.GetData(spawn.ItemId);
        if (itemData != null && rawFishData.TryGetValue(itemData.ItemId, out string? fishDataString))
        {
            var fields = fishDataString.Split('/');
            if (fields.Length > 1 && fields[1] == "trap")
                return false;
        }

        return true;
    }

    private sealed class FishEntry
    {
        public string ItemId { get; }
        public string QualifiedItemId { get; }
        public string DisplayName { get; }
        public Texture2D Texture { get; }
        public Rectangle SourceRect { get; }
        public List<LocationTime> LocationTimes { get; } = new();

        public FishEntry(string itemId, string qualifiedItemId, string displayName, Texture2D texture, Rectangle sourceRect)
        {
            ItemId = itemId;
            QualifiedItemId = qualifiedItemId;
            DisplayName = displayName;
            Texture = texture;
            SourceRect = sourceRect;
        }
    }

    private sealed record LocationTime(string Location, string TimeRange);
}
