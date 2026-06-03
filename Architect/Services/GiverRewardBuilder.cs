using System.Collections.Generic;
using System.Globalization;
using MoreQuestsFramework.Rewards;
using StardewValley;

namespace Architect.Services;

// Builds the reward: up to RewardItemCount distinct items the giver personally loves
// (not the universal-love list), topped up with items they like if they don't have
// enough loved ones. One copy of each.
internal static class GiverRewardBuilder
{
    public static List<RewardSpec> Build(string giver, ModConfig config)
    {
        var rewards = new List<RewardSpec>();
        var taste = LoadTaste(giver);
        if (taste == null)
            return Fallback();

        var (loved, liked) = taste.Value;
        var picked = new List<string>();
        var seen = new HashSet<string>();

        void TakeFrom(List<string> source)
        {
            foreach (string qualifiedId in source)
            {
                if (picked.Count >= config.RewardItemCount)
                    return;
                if (seen.Add(qualifiedId))
                    picked.Add(qualifiedId);
            }
        }

        TakeFrom(loved);
        TakeFrom(liked);

        if (picked.Count == 0)
            return Fallback();

        foreach (string id in picked)
            rewards.Add(new ObjectReward(id, 1));
        return rewards;
    }

    private static List<RewardSpec> Fallback()
        => new() { new MoneyReward(1500) };

    // Returns the giver's loved and liked concrete object ids (qualified), in order,
    // skipping category tags / negative ids and anything that doesn't resolve.
    private static (List<string> loved, List<string> liked)? LoadTaste(string giver)
    {
        Dictionary<string, string> data;
        try
        {
            data = Game1.content.Load<Dictionary<string, string>>("Data/NPCGiftTastes");
        }
        catch
        {
            return null;
        }

        if (!data.TryGetValue(giver, out string? row) || string.IsNullOrEmpty(row))
            return null;

        // Slash fields: 0 universal-love, 1 loved, 2 universal-like, 3 liked, ...
        var fields = row.Split('/');
        var loved = ParseConcreteItems(fields, 1);
        var liked = ParseConcreteItems(fields, 3);
        return (loved, liked);
    }

    private static List<string> ParseConcreteItems(string[] fields, int index)
    {
        var result = new List<string>();
        if (index >= fields.Length || string.IsNullOrWhiteSpace(fields[index]))
            return result;

        foreach (string token in fields[index].Split(' '))
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;
            string bare = StripPrefix(token);
            // Negative ids are gift-taste categories (all gems, all fruit, ...), not items.
            if (int.TryParse(bare, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric) && numeric < 0)
                continue;
            string qualified = token.StartsWith("(") ? token : "(O)" + bare;
            if (ItemRegistry.GetData(qualified) == null)
                continue;
            result.Add(qualified);
        }
        return result;
    }

    private static string StripPrefix(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length == 0 || id[0] != '(')
            return id;
        int close = id.IndexOf(')');
        return close > 0 && close < id.Length - 1 ? id.Substring(close + 1) : id;
    }
}
