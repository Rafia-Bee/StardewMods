using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace MoreQuestsFramework.Conditions;

// Top-level dictionary keys are AND-combined. Prefix a key with "not:" to negate.
// Values may use "|" to OR alternatives. "Season" also accepts a space-separated list
// for backwards compat.
public static class ConditionEvaluator
{
    public static bool MatchesSeason(string season) =>
        string.Equals(Game1.currentSeason, season, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesAnySeason(params string[] seasons)
    {
        for (int i = 0; i < seasons.Length; i++)
        {
            if (string.Equals(Game1.currentSeason, seasons[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool MinDaysPlayed(int days) => Game1.stats.DaysPlayed > (uint)days;

    public static int FarmingLevel => Game1.player.FarmingLevel;
    public static int FishingLevel => Game1.player.FishingLevel;
    public static int MiningLevel => Game1.player.MiningLevel;
    public static int ForagingLevel => Game1.player.ForagingLevel;
    public static int CombatLevel => Game1.player.CombatLevel;

    public static bool MinDeepestMineLevel(int level) => Game1.player.deepestMineLevel >= level;
    public static bool MineShaftReached(int level) => MineShaft.lowestLevelReached >= level;

    public static bool NpcExists(string name) => Game1.getCharacterFromName(name) != null;
    public static bool NpcMet(string name) => Game1.player.friendshipData.ContainsKey(name);

    // Unknown keys evaluate false (fail closed).
    public static bool Evaluate(IReadOnlyDictionary<string, string>? conditions, IModRegistry? modRegistry = null)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        foreach (var (key, value) in conditions)
        {
            bool negate = key.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
            string realKey = negate ? key[4..] : key;
            bool match = EvaluateAlternatives(realKey, value, modRegistry);
            if (negate ? match : !match)
                return false;
        }
        return true;
    }

    private static bool EvaluateAlternatives(string key, string value, IModRegistry? modRegistry)
    {
        if (!value.Contains('|'))
            return EvaluateOne(key, value, modRegistry);

        foreach (var alt in value.Split('|', StringSplitOptions.RemoveEmptyEntries))
            if (EvaluateOne(key, alt.Trim(), modRegistry))
                return true;
        return false;
    }

    private static bool EvaluateOne(string key, string value, IModRegistry? modRegistry)
    {
        switch (key.ToLowerInvariant())
        {
            case "season":
                foreach (var s in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (MatchesSeason(s)) return true;
                return false;

            case "date":
                return MatchesDate(value);

            case "dayrange":
                return MatchesDayRange(value);

            case "daysofweek":
                return MatchesDayOfWeek(value);

            case "year":
                return int.TryParse(value, out int yr) && Game1.year >= yr;

            case "weather":
                return MatchesWeather(Game1.netWorldState.Value.GetWeatherForLocation(Game1.player.currentLocation?.GetLocationContextId() ?? "Default")?.Weather, value);

            case "weatherforecast":
                return MatchesWeather(Game1.weatherForTomorrow, value);

            case "mindaysplayed":
                return int.TryParse(value, out int min) && MinDaysPlayed(min);

            case "maxdaysplayed":
                return int.TryParse(value, out int max) && Game1.stats.DaysPlayed <= (uint)max;

            case "isplayermarried":
                return ParseBool(value) == Game1.player.isMarriedOrRoommates();

            case "ismultiplayer":
                return ParseBool(value) == Game1.IsMultiplayer;

            case "iscommunitycentercompleted":
                return ParseBool(value) == Game1.player.hasCompletedCommunityCenter();

            case "skilllevel":
                return MatchesSkillLevel(value);

            case "mindeepestminelevel":
                return int.TryParse(value, out int dml) && MinDeepestMineLevel(dml);

            case "npcexists":
                // Space-separated value is AND. Use "|" for OR.
                foreach (var n in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (!NpcExists(n)) return false;
                return true;

            case "npcmet":
                foreach (var n in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (!NpcMet(n)) return false;
                return true;

            case "friendshiplevel":
                return MatchesFriendshipLevel(value);

            case "friendshipstatus":
                return MatchesFriendshipStatus(value);

            case "mailreceived":
                return Game1.player.mailReceived.Contains(value);

            case "eventseen":
                return Game1.player.eventsSeen.Contains(value);

            case "buildingexists":
                return BuildingExists(value);

            case "knowncookingrecipe":
                return Game1.player.cookingRecipes.ContainsKey(value);

            case "knowncraftingrecipe":
                return Game1.player.craftingRecipes.ContainsKey(value);

            case "statatleast":
                return MatchesStatAtLeast(value);

            case "shippedatleast":
                return MatchesShippedAtLeast(value);

            case "hasitemeverobtained":
                // Vanilla doesn't track this generically; fall back to shipped/cooked-at-least-1
                // which catches first-egg/first-milk style triggers.
                return Game1.player.basicShipped.ContainsKey(value)
                    || Game1.player.recipesCooked.ContainsKey(value);

            case "hasmod":
                return modRegistry != null && modRegistry.IsLoaded(value);

            case "followinganimalcount":
                return int.TryParse(value, out int needed)
                    && modRegistry != null
                    && modRegistry.IsLoaded(ModCompat.LivestockFollowsYou)
                    && FollowerApiBridge.CurrentCount() >= needed;

            case "random":
                return float.TryParse(value, out float pct) && Game1.random.NextDouble() < pct;

            case "gsq":
                return GameStateQuery.CheckConditions(value);

            default:
                return false;
        }
    }

    private static bool ParseBool(string value) =>
        bool.TryParse(value, out bool b) ? b : value == "1";

    // "spring 8" or "spring 8-12".
    private static bool MatchesDate(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;
        if (!string.Equals(parts[0], Game1.currentSeason, StringComparison.OrdinalIgnoreCase))
            return false;

        if (parts[1].Contains('-'))
        {
            var range = parts[1].Split('-');
            return range.Length == 2
                && int.TryParse(range[0], out int from)
                && int.TryParse(range[1], out int to)
                && Game1.dayOfMonth >= from && Game1.dayOfMonth <= to;
        }
        return int.TryParse(parts[1], out int day) && Game1.dayOfMonth == day;
    }

    // "1-7", season-agnostic day-of-month range.
    private static bool MatchesDayRange(string value)
    {
        var parts = value.Split('-');
        return parts.Length == 2
            && int.TryParse(parts[0], out int from)
            && int.TryParse(parts[1], out int to)
            && Game1.dayOfMonth >= from && Game1.dayOfMonth <= to;
    }

    // "Mon Wed Fri" or "1 3 5".
    private static bool MatchesDayOfWeek(string value)
    {
        var today = Game1.Date.DayOfWeek;
        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out int idx) && (int)today == idx % 7)
                return true;
            if (Enum.TryParse<DayOfWeek>(token, ignoreCase: true, out var dow) && dow == today)
                return true;
            if (token.Length >= 3 && Enum.TryParse<DayOfWeek>(ExpandShortDay(token), ignoreCase: true, out var dow2) && dow2 == today)
                return true;
        }
        return false;
    }

    private static string ExpandShortDay(string token) => token[..3].ToLowerInvariant() switch
    {
        "sun" => "Sunday",
        "mon" => "Monday",
        "tue" => "Tuesday",
        "wed" => "Wednesday",
        "thu" => "Thursday",
        "fri" => "Friday",
        "sat" => "Saturday",
        _ => token
    };

    // sunny/rainy/stormy/snowy/windy aliases map to vanilla Sun/Rain/Storm/Snow/Wind IDs.
    private static bool MatchesWeather(string? actual, string requested)
    {
        if (string.IsNullOrEmpty(actual))
            return false;
        string norm = requested.ToLowerInvariant() switch
        {
            "sunny" => "Sun",
            "rainy" => "Rain",
            "stormy" => "Storm",
            "snowy" => "Snow",
            "windy" => "Wind",
            _ => requested
        };
        return string.Equals(actual, norm, StringComparison.OrdinalIgnoreCase);
    }

    // "Farming 5". "cooking" reads SpaceCore's spacechase0.Cooking and returns 0 when
    // no cooking-skill mod is installed (so "Cooking N" with N>=1 fails closed).
    private static bool MatchesSkillLevel(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int min))
            return false;
        int actual = parts[0].ToLowerInvariant() switch
        {
            "farming" => FarmingLevel,
            "fishing" => FishingLevel,
            "mining" => MiningLevel,
            "foraging" => ForagingLevel,
            "combat" => CombatLevel,
            "cooking" => SpaceCoreSkills.GetLevel(Game1.player, "spacechase0.Cooking"),
            _ => -1
        };
        return actual >= min;
    }

    // "Abigail 2" = 2 hearts (250 points per heart).
    private static bool MatchesFriendshipLevel(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int hearts))
            return false;
        if (!Game1.player.friendshipData.TryGetValue(parts[0], out var f))
            return false;
        return f.Points >= hearts * 250;
    }

    // "Abigail dating|married|engaged|roommate|divorced".
    private static bool MatchesFriendshipStatus(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;
        if (!Game1.player.friendshipData.TryGetValue(parts[0], out var f))
            return false;
        return parts[1].ToLowerInvariant() switch
        {
            "dating" => f.IsDating(),
            "engaged" => f.IsEngaged(),
            "married" => f.IsMarried(),
            "roommate" => f.RoommateMarriage,
            "divorced" => f.IsDivorced(),
            _ => false
        };
    }

    // Counts buildings on the farm only.
    private static bool BuildingExists(string buildingType)
    {
        var farm = Game1.getFarm();
        if (farm == null)
            return false;
        foreach (var b in farm.buildings)
            if (string.Equals(b.buildingType.Value, buildingType, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // "ChickenEggsLayed 1", "MonstersKilled 50", etc. Reads Game1.stats.
    private static bool MatchesStatAtLeast(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint min))
            return false;
        return Game1.stats.Get(parts[0]) >= min;
    }

    // "(O)174 1" or "174 1": item id + minimum shipped count.
    private static bool MatchesShippedAtLeast(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int min))
            return false;
        string itemId = parts[0].StartsWith("(O)") ? parts[0][3..] : parts[0];
        return Game1.player.basicShipped.TryGetValue(itemId, out int count) && count >= min;
    }
}
