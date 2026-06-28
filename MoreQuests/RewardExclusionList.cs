using System;
using System.Collections.Generic;
using StardewValley;

namespace MoreQuests;

/// Items the player should never get as a quest reward, and (when the matching toggle is
/// on) never be asked to hand over in item-delivery or shipping quests. The list is the
/// comma-separated ModConfig.RewardExclusionItemIds, parsed once and re-parsed only when
/// the player edits it. Wired into the framework via RewardApplier.ObjectRewardExclusion
/// (rewards, always on) and ItemResolver.RequestItemExclusion (requests, opt-in).
/// Distinct from QuestItemBlacklist, which keeps rare items out of objective pools but
/// still lets them be handed over as rewards.
internal static class RewardExclusionList
{
    private static string? _parsedFrom;
    private static HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Ids()
    {
        string raw = ModEntry.Config?.RewardExclusionItemIds ?? string.Empty;
        if (!string.Equals(raw, _parsedFrom, StringComparison.Ordinal))
        {
            _ids = Parse(raw);
            _parsedFrom = raw;
        }
        return _ids;
    }

    // Stores both the raw token and its qualified form so a config entry written either
    // way ("74" or "(O)74") still matches a qualified reward / request id.
    private static HashSet<string> Parse(string raw)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ids.Add(token);
            string qualified = ItemRegistry.QualifyItemId(token) ?? token;
            ids.Add(qualified);
        }
        return ids;
    }

    /// Reward side: always enforced so an excluded item never lands in the player's bag.
    public static bool IsExcludedReward(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;
        var ids = Ids();
        if (ids.Count == 0)
            return false;
        if (ids.Contains(itemId))
            return true;
        string qualified = ItemRegistry.QualifyItemId(itemId) ?? itemId;
        return ids.Contains(qualified);
    }

    /// Request side: only enforced when the player turns on ExcludeListAppliesToRequests.
    public static bool IsExcludedRequest(string itemId)
    {
        if (!(ModEntry.Config?.ExcludeListAppliesToRequests ?? false))
            return false;
        return IsExcludedReward(itemId);
    }
}
