using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace MoreQuestsFramework.Conditions;

/// Central evaluator for "is this condition true right now?" queries used by quest
/// availability, trigger gating, and JSON `Available { ... }` blocks (Phase 3+).
///
/// The static helpers cover the common cases each quest's `IsAvailable` calls
/// directly. `Evaluate(dict, modRegistry)` is the dictionary-driven entry point
/// for declarative specs; it covers every key in plan.md §2.6 plus `GSQ` (the
/// 1.6 GameStateQuery escape hatch).
///
/// Top-level dictionary keys are AND-combined. A key may be prefixed with `not:`
/// to negate. Values may use `|` as an OR-combinator (e.g. `"Season": "spring|fall"`
/// matches either season). For backwards compatibility, `Season` also accepts a
/// space-separated list.
public static class ConditionEvaluator
{
    // -------- Calendar / time --------

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

    // -------- Skills / mine progress --------

    public static int FarmingLevel => Game1.player.FarmingLevel;
    public static int FishingLevel => Game1.player.FishingLevel;
    public static int MiningLevel => Game1.player.MiningLevel;
    public static int ForagingLevel => Game1.player.ForagingLevel;
    public static int CombatLevel => Game1.player.CombatLevel;

    public static bool MinDeepestMineLevel(int level) => Game1.player.deepestMineLevel >= level;
    public static bool MineShaftReached(int level) => MineShaft.lowestLevelReached >= level;

    // -------- NPCs --------

    public static bool NpcExists(string name) => Game1.getCharacterFromName(name) != null;
    public static bool NpcMet(string name) => Game1.player.friendshipData.ContainsKey(name);

    // -------- Recipes / inventory --------

    public static bool KnowsAnyCookingRecipe() => Game1.player.cookingRecipes.Length > 0;

    // -------- Dictionary-driven evaluation --------

    /// Evaluates a flat condition dictionary. Top-level keys are AND-combined; a key
    /// prefixed with `not:` is negated. Values may include `|` to OR alternatives.
    /// Unknown keys evaluate to false (and the whole AND chain fails) - safer to fail
    /// closed than to silently match.
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

    /// Splits the value on `|` and returns true if any alternative matches.
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
            // ---- calendar ----
            case "season":
                // Accept space-separated lists for compat with plan.md §5.2 examples.
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

            // ---- player progress ----
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

            // ---- npcs / friendship ----
            case "npcexists":
                // Space-separated value is AND ("George Evelyn" = both exist). Use `|` for OR.
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

            // ---- mail / events ----
            case "mailreceived":
                return Game1.player.mailReceived.Contains(value);

            case "eventseen":
                return Game1.player.eventsSeen.Contains(value);

            // ---- buildings ----
            case "buildingexists":
                return BuildingExists(value);

            // ---- recipes ----
            case "knowncookingrecipe":
                return Game1.player.cookingRecipes.ContainsKey(value);

            case "knowncraftingrecipe":
                return Game1.player.craftingRecipes.ContainsKey(value);

            // ---- stats / shipping / item history ----
            case "statatleast":
                return MatchesStatAtLeast(value);

            case "shippedatleast":
                return MatchesShippedAtLeast(value);

            case "hasitemeverobtained":
                // Vanilla doesn't track this generically. Phase 6 lands an `ItemHistoryWatcher`
                // (plan.md §7) that will populate save data. Until then, fall back to a cheap
                // heuristic: shipped-at-least-1, which catches first-egg/first-milk style triggers.
                return Game1.player.basicShipped.ContainsKey(value)
                    || Game1.player.recipesCooked.ContainsKey(value);

            // ---- mods / RNG / GSQ ----
            case "hasmod":
                return modRegistry != null && modRegistry.IsLoaded(value);

            case "followinganimalcount":
                // Livestock Follows You integration. Without that mod loaded, count is 0.
                // Phase 5 plans to expose this as a registered condition handler.
                return int.TryParse(value, out int needed)
                    && modRegistry != null
                    && modRegistry.IsLoaded(ModCompat.LivestockFollowsYou)
                    && needed <= 0;

            case "random":
                return float.TryParse(value, out float pct) && Game1.random.NextDouble() < pct;

            case "gsq":
                return GameStateQuery.CheckConditions(value);

            default:
                return false;
        }
    }

    // ----- helpers -----

    private static bool ParseBool(string value) =>
        bool.TryParse(value, out bool b) ? b : value == "1";

    /// `"spring 8"` or `"spring 8-12"` - matches today's date.
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

    /// `"1-7"` - day-of-month range, season-agnostic.
    private static bool MatchesDayRange(string value)
    {
        var parts = value.Split('-');
        return parts.Length == 2
            && int.TryParse(parts[0], out int from)
            && int.TryParse(parts[1], out int to)
            && Game1.dayOfMonth >= from && Game1.dayOfMonth <= to;
    }

    /// `"Mon Wed Fri"` or `"1 3 5"` - matches today's day-of-week.
    private static bool MatchesDayOfWeek(string value)
    {
        var today = Game1.Date.DayOfWeek; // System.DayOfWeek
        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out int idx) && (int)today == idx % 7)
                return true;
            if (Enum.TryParse<DayOfWeek>(token, ignoreCase: true, out var dow) && dow == today)
                return true;
            // Three-letter shorthand: Mon, Tue, ...
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

    /// `"sunny"`, `"rain"`, `"storm"`, `"snow"`, `"wind"` (case-insensitive). Vanilla weather
    /// IDs are: Sun, Rain, Storm, Snow, Wind, Festival, Wedding, GreenRain.
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

    /// `"Farming 5"` - skill name + minimum level.
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
            _ => -1
        };
        return actual >= min;
    }

    /// `"Abigail 2"` - NPC + minimum hearts (1 heart = 250 friendship points).
    private static bool MatchesFriendshipLevel(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int hearts))
            return false;
        if (!Game1.player.friendshipData.TryGetValue(parts[0], out var f))
            return false;
        return f.Points >= hearts * 250;
    }

    /// `"Abigail dating"` / `"Abigail married"` / `"Abigail engaged"` / `"Abigail roommate"`.
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

    /// `"Coop"`, `"Big Coop"`, `"Deluxe Coop"`, `"Barn"`, `"Big Barn"`, `"Deluxe Barn"`,
    /// `"Silo"`, `"Stable"`, `"Mill"`, `"Slime Hutch"`, `"Shed"`, etc. Counts buildings on
    /// the farm only.
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

    /// `"ChickenEggsLayed 1"`, `"MonstersKilled 50"`, etc. Reads from `Game1.stats.Values`.
    private static bool MatchesStatAtLeast(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint min))
            return false;
        return Game1.stats.Get(parts[0]) >= min;
    }

    /// `"(O)174 1"` (qualified ID + minimum count) or `"174 1"` (bare ID).
    private static bool MatchesShippedAtLeast(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int min))
            return false;
        string itemId = parts[0].StartsWith("(O)") ? parts[0][3..] : parts[0];
        return Game1.player.basicShipped.TryGetValue(itemId, out int count) && count >= min;
    }
}
