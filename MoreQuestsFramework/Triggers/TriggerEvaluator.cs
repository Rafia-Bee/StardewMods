using System;
using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Triggers;

// Non-pool triggers evaluated at DayStarted. Calendar triggers are per-day; event
// triggers (Building/Mail/OneShot) are detected by diffing yesterday's snapshot.
// NpcDialogue is the runtime-watcher exception, handled by DialogueWatcher.
public sealed class TriggerEvaluator
{
    private readonly FrameworkState _state;
    private readonly IMonitor _monitor;

    private readonly HashSet<string> _newBuildingsToday = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> NewBuildingsToday => _newBuildingsToday;

    private readonly HashSet<string> _newMailFlagsToday = new(StringComparer.OrdinalIgnoreCase);

    public TriggerEvaluator(FrameworkState state, IMonitor monitor)
    {
        _state = state;
        _monitor = monitor;
    }

    public void BeginDay()
    {
        _newBuildingsToday.Clear();
        _newMailFlagsToday.Clear();

        var farm = Game1.getFarm();
        var current = new List<string>();
        if (farm != null)
        {
            foreach (var b in farm.buildings)
                current.Add(b.buildingType.Value);
        }
        var prev = new HashSet<string>(_state.LastSeenBuildings, StringComparer.OrdinalIgnoreCase);
        foreach (var t in current)
            if (!prev.Contains(t))
                _newBuildingsToday.Add(t);
        _state.LastSeenBuildings = current;

        if (Game1.player != null)
        {
            var prevMail = new HashSet<string>(_state.LastSeenMailFlags, StringComparer.OrdinalIgnoreCase);
            var currentMail = new List<string>();
            foreach (var flag in Game1.player.mailReceived)
            {
                currentMail.Add(flag);
                if (!prevMail.Contains(flag))
                    _newMailFlagsToday.Add(flag);
            }
            _state.LastSeenMailFlags = currentMail;
        }
    }

    public bool ShouldFireToday(string defId, TriggerSource source, TriggerInfo info, int cooldownDays)
    {
        int today = Game1.Date.TotalDays;

        if (_state.ScheduledFireDay.TryGetValue(defId, out int dueDay))
        {
            if (today < dueDay)
                return false;
            _state.ScheduledFireDay.Remove(defId);
            RecordFire(defId, today);
            return true;
        }

        bool fired = source switch
        {
            TriggerSource.Mail => RespectCooldown(defId, cooldownDays),
            TriggerSource.Periodic => PeriodicReady(defId, info.EveryDays ?? 0),
            TriggerSource.DateLocked => DateLockedToday(defId, info.Date, info.RepeatYearly),
            TriggerSource.DateRange => DateRangeToday(info.From, info.To),
            TriggerSource.OneShot => OneShotJustFired(defId, info.When),
            TriggerSource.BuildingBuilt => BuildingTriggered(defId, info),
            TriggerSource.MailReceived => MailTriggered(defId, info),
            TriggerSource.WeatherForecast => WeatherForecastMatches(info.Weather),
            TriggerSource.NpcDialogue => false,
            TriggerSource.SpecialOrder => SpecialOrderReady(defId, info.StartDate, cooldownDays, info.Weight ?? 0),
            _ => false
        };

        if (fired)
            RecordFire(defId, today);
        return fired;
    }

    private void RecordFire(string defId, int today) => _state.LastFiredDay[defId] = today;

    private bool RespectCooldown(string defId, int cooldownDays)
    {
        if (cooldownDays <= 0)
            return true;
        if (!_state.LastFiredDay.TryGetValue(defId, out int last))
            return true;
        return Game1.Date.TotalDays - last >= cooldownDays;
    }

    private bool PeriodicReady(string defId, int everyDays)
    {
        if (everyDays <= 0)
            return false;
        if (!_state.LastFiredDay.TryGetValue(defId, out int last))
            return true;
        return Game1.Date.TotalDays - last >= everyDays;
    }

    private bool DateLockedToday(string defId, string? date, bool repeatYearly)
    {
        if (string.IsNullOrWhiteSpace(date))
            return false;
        if (!ParseDate(date, out string season, out int day))
            return false;
        if (Game1.dayOfMonth != day || !string.Equals(Game1.currentSeason, season, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!_state.LastFiredDay.TryGetValue(defId, out int last))
            return true;
        if (!repeatYearly)
            return false;
        int lastYear = (last - 1) / 112 + 1;
        return Game1.year > lastYear;
    }

    private bool DateRangeToday(string? from, string? to)
    {
        if (!ParseDate(from, out string fromSeason, out int fromDay))
            return false;
        if (!ParseDate(to, out string toSeason, out int toDay))
            return false;
        int now = SeasonDayIndex(Game1.currentSeason, Game1.dayOfMonth);
        int start = SeasonDayIndex(fromSeason, fromDay);
        int end = SeasonDayIndex(toSeason, toDay);
        return now >= start && now <= end;
    }

    private bool OneShotJustFired(string defId, string? when)
    {
        if (_state.OneShotFired.TryGetValue(defId, out bool already) && already)
            return false;
        if (string.IsNullOrWhiteSpace(when))
            return false;
        if (!EvaluateOneShotPredicate(when))
            return false;
        _state.OneShotFired[defId] = true;
        return true;
    }

    private bool BuildingTriggered(string defId, TriggerInfo info)
    {
        if (string.IsNullOrEmpty(info.Building))
            return false;
        if (!_newBuildingsToday.Contains(info.Building))
            return false;
        int delay = Math.Max(0, info.DayDelay ?? 0);
        if (delay > 0)
        {
            _state.ScheduledFireDay[defId] = Game1.Date.TotalDays + delay;
            return false;
        }
        return true;
    }

    private bool MailTriggered(string defId, TriggerInfo info)
    {
        if (string.IsNullOrEmpty(info.Flag))
            return false;
        if (!_newMailFlagsToday.Contains(info.Flag))
            return false;
        int delay = Math.Max(0, info.DayDelay ?? 0);
        if (delay > 0)
        {
            _state.ScheduledFireDay[defId] = Game1.Date.TotalDays + delay;
            return false;
        }
        return true;
    }

    // Date-locked (StartDate set) or cooldown-only (Sunday + cooldown + weight% roll).
    private bool SpecialOrderReady(string defId, string? startDate, int cooldownDays, int weight)
    {
        if (!string.IsNullOrWhiteSpace(startDate))
        {
            if (!ParseDate(startDate, out string season, out int day))
                return false;
            if (Game1.dayOfMonth != day || !string.Equals(Game1.currentSeason, season, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!_state.LastFiredDay.TryGetValue(defId, out int last))
                return true;
            if (cooldownDays <= 0)
                return true;
            return Game1.Date.TotalDays - last >= cooldownDays;
        }

        if (weight <= 0)
            return false;
        if (Game1.Date.DayOfWeek != System.DayOfWeek.Sunday)
            return false;
        if (cooldownDays > 0 && _state.LastFiredDay.TryGetValue(defId, out int lastFired)
            && Game1.Date.TotalDays - lastFired < cooldownDays)
            return false;
        return Game1.random.Next(100) < weight;
    }

    private static bool WeatherForecastMatches(string? weather)
    {
        if (string.IsNullOrEmpty(weather))
            return false;
        string tomorrow = Game1.weatherForTomorrow ?? "";
        string norm = weather.ToLowerInvariant() switch
        {
            "sunny" => "Sun",
            "rainy" => "Rain",
            "stormy" => "Storm",
            "snowy" => "Snow",
            "windy" => "Wind",
            _ => weather
        };
        return string.Equals(tomorrow, norm, StringComparison.OrdinalIgnoreCase);
    }

    // Forms: "FirstStat <name>[|<name>...] >= <n>", "FirstShipped <id>",
    // "FirstItemOwned <id>" (proxied to basicShipped + recipesCooked), "FirstHeldItem <id>",
    // "FirstCraftingRecipe <Name>", "FirstCookingRecipe <Name>".
    private bool EvaluateOneShotPredicate(string when)
    {
        var parts = when.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        switch (parts[0].ToLowerInvariant())
        {
            case "firststat":
                if (parts.Length < 4 || parts[2] != ">=" || !uint.TryParse(parts[3], out uint min))
                    return false;
                foreach (var statName in parts[1].Split('|', StringSplitOptions.RemoveEmptyEntries))
                    if (Game1.stats.Get(statName.Trim()) >= min)
                        return true;
                return false;

            case "firstshipped":
                if (parts.Length < 2)
                    return false;
                return Game1.player.basicShipped.ContainsKey(StripPrefix(parts[1]));

            case "firstitemowned":
                if (parts.Length < 2)
                    return false;
                string id = StripPrefix(parts[1]);
                return Game1.player.basicShipped.ContainsKey(id)
                    || Game1.player.recipesCooked.ContainsKey(id);

            case "firsthelditem":
                if (parts.Length < 2)
                    return false;
                return PlayerInventoryContains(parts[1]);

            case "firstcraftingrecipe":
                // Joins remaining tokens so multi-word names ("Preserves Jar") don't need quoting.
                if (parts.Length < 2)
                    return false;
                string craftingName = string.Join(' ', parts, 1, parts.Length - 1);
                return Game1.player.craftingRecipes.ContainsKey(craftingName);

            case "firstcookingrecipe":
                if (parts.Length < 2)
                    return false;
                string cookingName = string.Join(' ', parts, 1, parts.Length - 1);
                return Game1.player.cookingRecipes.ContainsKey(cookingName);

            default:
                _monitor.Log($"OneShot 'When' clause not recognised: '{when}'.", LogLevel.Warn);
                return false;
        }
    }

    private static bool PlayerInventoryContains(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested) || Game1.player == null)
            return false;
        string qualified = StardewValley.ItemRegistry.QualifyItemId(requested) ?? requested;
        string bare = StripPrefix(requested);
        foreach (var entry in Game1.player.Items)
        {
            if (entry == null) continue;
            if (string.Equals(entry.QualifiedItemId, qualified, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(entry.ItemId, bare, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string StripPrefix(string id) =>
        id.StartsWith("(O)", StringComparison.Ordinal) ? id[3..] : id;

    private static bool ParseDate(string? value, out string season, out int day)
    {
        season = "";
        day = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out day))
            return false;
        season = parts[0];
        return day >= 1 && day <= 28;
    }

    private static int SeasonDayIndex(string season, int day)
    {
        int seasonIdx = season.ToLowerInvariant() switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => 0
        };
        return seasonIdx * 28 + (day - 1);
    }
}
