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
using StardewValley.Internal;
using StardewValley.Menus;

namespace CatchOfTheDay.Framework;

/// <summary>
/// Renders weather-exclusive fish icons on the right side of the HUD.
/// Hovering over an icon shows spawn locations and catchable time windows.
/// </summary>
internal sealed class WeatherFishHud
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly Func<ModConfig> _getConfig;
    private readonly FishHudOverlay _overlay;

    private List<FishEntry> _entries = new();
    private Dictionary<string, List<string>> _bundleNeeds = new();
    private int _lastFishCaughtCount = -1;
    private string? _hoveredFishId;

    public string? HoveredFishId => _hoveredFishId;
    public string? HoveredFishName { get; private set; }

    private static readonly HashSet<string> IgnoreTimeKeys =
        new(StringComparer.OrdinalIgnoreCase) { "TIME" };

    private const int BaseIconSize = 40;
    private const int IconPadding = 5;
    private const int NightStartTime = 1800;
    private const int MorningEndTime = 1200;

    public WeatherFishHud(IModHelper helper, IMonitor monitor, Func<ModConfig> getConfig, FishHudOverlay overlay)
    {
        _helper = helper;
        _monitor = monitor;
        _getConfig = getConfig;
        _overlay = overlay;
    }

    public void Clear()
    {
        _entries.Clear();
        _bundleNeeds.Clear();
    }

    public void Refresh()
    {
        _entries.Clear();
        _bundleNeeds.Clear();

        if (!Context.IsWorldReady)
            return;

        RefreshBundleNeeds();

        var config = _getConfig();
        var fishMap = new Dictionary<string, FishEntry>();
        var rawFishData = DataLoader.Fish(Game1.content);

        foreach (string locName in Game1.player.locationsVisited)
        {
            var location = Game1.getLocationFromName(locName);
            if (location == null)
                continue;

            string currentWeather = GetLocationWeather(location);
            CollectFish(location, locName, rawFishData, fishMap, currentWeather, config);
        }

        _entries = fishMap.Values.OrderBy(f => f.DisplayName).ToList();
    }

    private void RefreshBundleNeeds()
    {
        var bundleData = Game1.netWorldState.Value.BundleData;
        var bundles = Game1.netWorldState.Value.Bundles;

        foreach (var kvp in bundleData)
        {
            string[] keyParts = kvp.Key.Split('/');
            if (keyParts.Length < 2 || !int.TryParse(keyParts[1], out int bundleIndex))
                continue;

            string[] fields = kvp.Value.Split('/');
            if (fields.Length < 7)
                continue;

            string bundleDisplayName = fields[6];
            string[] ingredients = ArgUtility.SplitBySpace(fields[2]);

            if (!bundles.TryGetValue(bundleIndex, out bool[] slots))
                continue;

            bool allSlotsDone = slots.Length > 0 && slots.All(s => s);
            if (allSlotsDone)
                continue;

            for (int i = 0; i < ingredients.Length - 2; i += 3)
            {
                int slotIndex = i / 3;
                if (slotIndex < slots.Length && slots[slotIndex])
                    continue;

                string rawId = ingredients[i];
                if (int.TryParse(rawId, out int num) && num < 0)
                    continue;

                var parsed = ItemRegistry.GetData(rawId);
                string qualifiedId = parsed?.QualifiedItemId ?? ("(O)" + rawId);

                if (!_bundleNeeds.TryGetValue(qualifiedId, out var list))
                {
                    list = new List<string>();
                    _bundleNeeds[qualifiedId] = list;
                }

                if (!list.Contains(bundleDisplayName))
                    list.Add(bundleDisplayName);
            }
        }
    }

    public void Draw(SpriteBatch b)
    {
        if (!Context.IsWorldReady)
            return;

        if (_getConfig().HideAlreadyCaught)
        {
            int count = Game1.player.fishCaught.Length;
            if (count != _lastFishCaughtCount)
            {
                _lastFishCaughtCount = count;
                Refresh();
            }
        }

        if (_entries.Count == 0)
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
        int iconSize = (int)(BaseIconSize * config.IconScale);

        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        FishEntry? hovered = null;

        for (int i = 0; i < _entries.Count; i++)
        {
            var fish = _entries[i];
            int x = horizontal ? baseX + i * (iconSize + IconPadding) : baseX;
            int y = horizontal ? baseY : baseY + i * (iconSize + IconPadding);
            var dest = new Rectangle(x, y, iconSize, iconSize);

            bool isHover = dest.Contains(mouseX, mouseY);
            if (isHover)
                hovered = fish;

            b.Draw(Game1.staminaRect, dest, Color.Black * 0.25f);

            Color bgTint = GetIconBackground(fish, config);
            if (bgTint.A > 0)
                b.Draw(Game1.staminaRect, dest, bgTint);

            if (isHover)
                dest.Inflate(3, 3);

            b.Draw(fish.Texture, dest, fish.SourceRect, Color.White);
        }

        _overlay.HoveredItem = hovered != null
            ? ItemRegistry.Create(hovered.QualifiedItemId)
            : null;

        _hoveredFishId = hovered?.QualifiedItemId;
        HoveredFishName = hovered?.DisplayName;

        if (hovered != null)
            DrawTooltip(b, hovered);
    }

    private void DrawTooltip(SpriteBatch b, FishEntry fish)
    {
        var sb = new StringBuilder();
        if (!Game1.player.fishCaught.ContainsKey(fish.QualifiedItemId))
            sb.AppendLine("(Uncaught)");

        if (fish.SellPrice > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append($"Sells for {fish.SellPrice}g");
        }

        if (_getConfig().ShowBundleNeeds && _bundleNeeds.TryGetValue(fish.QualifiedItemId, out var bundles))
        {
            foreach (string bundleName in bundles)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append($"Needed: {bundleName} Bundle");
            }
        }

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
        Dictionary<string, FishEntry> fishMap,
        string currentWeather,
        ModConfig config)
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

            var itemData = ItemRegistry.GetData(spawn.ItemId);
            if (itemData == null)
                continue;

            string? weatherReq = GetWeatherRequirement(spawn, rawFishData);

            if (weatherReq != null)
            {
                if (!WeatherMatchesCurrent(currentWeather, weatherReq))
                    continue;
                if (!IsWeatherTracked(weatherReq, config))
                    continue;
            }
            else
            {
                var windows = GetTimeWindows(itemData.ItemId, rawFishData);
                bool isNightOnly = config.ShowNightFish
                    && windows.Count > 0 && windows.All(w => w.Start >= NightStartTime);
                bool isMorningOnly = config.ShowMorningFish
                    && windows.Count > 0 && windows.All(w => w.End <= MorningEndTime);

                if (!isNightOnly && !isMorningOnly)
                    continue;
            }

            if (config.HideAlreadyCaught && Game1.player.fishCaught.ContainsKey(itemData.QualifiedItemId))
                continue;

            if (config.HiddenFishIds.Contains(itemData.QualifiedItemId))
                continue;

            int sellPrice = 0;
            var tempItem = ItemRegistry.Create(itemData.QualifiedItemId);
            if (tempItem is StardewValley.Object obj)
                sellPrice = obj.sellToStorePrice();

            if (config.MinSellPrice > 0 && sellPrice < config.MinSellPrice)
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
                    itemData.GetSourceRect(),
                    sellPrice,
                    GetTimeWindows(itemData.ItemId, rawFishData)
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

    private static List<(int Start, int End)> GetTimeWindows(string itemId, Dictionary<string, string> rawFishData)
    {
        var windows = new List<(int, int)>();
        if (!rawFishData.TryGetValue(itemId, out string? fishStr))
            return windows;

        var fields = fishStr.Split('/');
        if (fields.Length <= 5)
            return windows;

        var parts = fields[5].Split(' ');
        if (parts.Length < 2 || parts.Length % 2 != 0)
            return windows;

        for (int i = 0; i < parts.Length; i += 2)
        {
            if (int.TryParse(parts[i], out int start) && int.TryParse(parts[i + 1], out int end))
                windows.Add((start, end));
        }

        return windows;
    }

    private static Color GetIconBackground(FishEntry fish, ModConfig config)
    {
        int currentTime = Game1.timeOfDay;

        bool catchableNow = fish.TimeWindows.Count == 0
            || fish.TimeWindows.Any(w => IsTimeInWindow(currentTime, w.Start, w.End));

        if (catchableNow)
            return ParseHexColor(config.CatchableNowColor);

        bool isNightFish = fish.TimeWindows.Count > 0
            && fish.TimeWindows.All(w => w.Start >= NightStartTime);

        if (isNightFish)
            return ParseHexColor(config.NightFishColor);

        bool isMorningFish = fish.TimeWindows.Count > 0
            && fish.TimeWindows.All(w => w.End <= MorningEndTime);

        if (isMorningFish)
            return ParseHexColor(config.MorningFishColor);

        return Color.Transparent;
    }

    private static bool IsTimeInWindow(int time, int start, int end)
    {
        if (end > start)
            return time >= start && time < end;
        return time >= start || time < end;
    }

    private static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
            return Color.Transparent;

        hex = hex.Substring(1);
        if (hex.Length == 6)
            hex += "FF";
        if (hex.Length != 8)
            return Color.Transparent;

        try
        {
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            int a = Convert.ToInt32(hex.Substring(6, 2), 16);
            return new Color(r, g, b, a);
        }
        catch
        {
            return Color.Transparent;
        }
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

    private static string GetLocationWeather(GameLocation location)
    {
        if (location.IsGreenRainingHere()) return "GreenRain";
        if (location.IsLightningHere()) return "Storm";
        if (location.IsRainingHere()) return "Rain";
        if (location.IsSnowingHere()) return "Snow";
        if (location.IsDebrisWeatherHere()) return "Wind";
        return "Sun";
    }

    private static string? GetWeatherRequirement(SpawnFishData spawn, Dictionary<string, string> rawFishData)
    {
        if (!string.IsNullOrEmpty(spawn.Condition))
        {
            foreach (var cond in spawn.Condition.Split(',', StringSplitOptions.TrimEntries))
            {
                if (!cond.Contains("WEATHER", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool negated = cond.TrimStart().StartsWith('!');

                if (negated)
                {
                    if (cond.Contains("Sun", StringComparison.OrdinalIgnoreCase))
                        return "NotSun";
                    continue;
                }

                if (cond.Contains("GreenRain", StringComparison.OrdinalIgnoreCase))
                    return "GreenRain";
                if (cond.Contains("Snow", StringComparison.OrdinalIgnoreCase))
                    return "Snow";
                if (cond.Contains("Wind", StringComparison.OrdinalIgnoreCase))
                    return "Wind";
                if (cond.Contains("Rain", StringComparison.OrdinalIgnoreCase))
                    return "Rain";
                if (cond.Contains("Storm", StringComparison.OrdinalIgnoreCase))
                    return "Storm";
                if (cond.Contains("Sun", StringComparison.OrdinalIgnoreCase))
                    return "Sun";
            }
        }

        var itemData = ItemRegistry.GetData(spawn.ItemId);
        if (itemData != null && rawFishData.TryGetValue(itemData.ItemId, out string? fishStr))
        {
            var fields = fishStr.Split('/');
            if (fields.Length > 7)
            {
                if (fields[7] == "rainy") return "Rain";
                if (fields[7] == "sunny") return "Sun";
            }
        }

        return null;
    }

    private static bool WeatherMatchesCurrent(string currentWeather, string requirement)
    {
        if (requirement == "NotSun")
            return !currentWeather.Equals("Sun", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(currentWeather, requirement, StringComparison.OrdinalIgnoreCase))
            return true;
        if (requirement.Equals("Rain", StringComparison.OrdinalIgnoreCase)
            && currentWeather.Equals("Storm", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool IsWeatherTracked(string requirement, ModConfig config)
    {
        return requirement switch
        {
            "Rain" => config.TrackRain,
            "Storm" => config.TrackStorm,
            "Sun" => config.TrackSun,
            "Snow" => config.TrackSnow,
            "Wind" => config.TrackWind,
            "GreenRain" => config.TrackGreenRain,
            "NotSun" => config.TrackRain || config.TrackStorm || config.TrackSnow
                        || config.TrackWind || config.TrackGreenRain,
            _ => true
        };
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
        public int SellPrice { get; }
        public List<(int Start, int End)> TimeWindows { get; }
        public List<LocationTime> LocationTimes { get; } = new();

        public FishEntry(string itemId, string qualifiedItemId, string displayName, Texture2D texture, Rectangle sourceRect, int sellPrice, List<(int Start, int End)> timeWindows)
        {
            ItemId = itemId;
            QualifiedItemId = qualifiedItemId;
            DisplayName = displayName;
            Texture = texture;
            SourceRect = sourceRect;
            SellPrice = sellPrice;
            TimeWindows = timeWindows;
        }
    }

    private sealed record LocationTime(string Location, string TimeRange);
}
