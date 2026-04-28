using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Locations;

namespace MoreQuests.Framework.Conditions;

/// Central evaluator for "is this condition true right now?" queries used by quest
/// availability, trigger gating, and (Phase 3) JSON `Available { ... }` blocks.
///
/// Today the project mostly calls the static helpers directly from each quest's
/// `IsAvailable` method. The `Evaluate(dict)` overload is the future entry point
/// for JSON-driven specs and is a strict subset of what the helpers cover.
internal static class ConditionEvaluator
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

    // -------- Dictionary-driven evaluation (used by JSON specs in Phase 3+) --------

    /// Evaluates a flat condition dictionary. Unknown keys are treated as "not satisfied"
    /// and logged once per quest by the caller. Top-level keys are AND-combined; values
    /// are stringly-typed and parsed per condition.
    public static bool Evaluate(IReadOnlyDictionary<string, string>? conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        foreach (var (key, value) in conditions)
        {
            bool negate = key.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
            string realKey = negate ? key[4..] : key;
            bool match = EvaluateOne(realKey, value);
            if (negate ? match : !match)
                return false;
        }
        return true;
    }

    private static bool EvaluateOne(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "season":
                foreach (var s in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (MatchesSeason(s)) return true;
                return false;

            case "mindaysplayed":
                return int.TryParse(value, out int min) && MinDaysPlayed(min);

            case "mindeepestminelevel":
                return int.TryParse(value, out int dml) && MinDeepestMineLevel(dml);

            case "npcexists":
                return NpcExists(value);

            case "npcmet":
                return NpcMet(value);

            default:
                return false;
        }
    }
}
