using System.Collections.Generic;
using QuestJournal.Api;
using StardewValley.Quests;

namespace QuestJournal.Menu;

// Shared snapshot builders for QuestJournal. Both the journal panel
// (JournalContext) and the natural-completion watcher need to derive the
// same reward lines / giver / source for a given Quest. Keeping this
// centralised avoids the two paths drifting apart and capturing
// different fields for the same quest.
internal static class QuestSnapshotBuilder
{
    public static List<RewardLineRow> BuildRewardLines(Quest q, IMoreQuestsApi? mqfApi)
    {
        var lines = new List<RewardLineRow>();

        // Always ask MQF first, regardless of IsManagedQuest. The framework's
        // _managed table is a ConditionalWeakTable populated only at quest
        // creation, so save-reloaded quests fall out of it even though their
        // SerializedRewards (NetStringList) survives the save trip. Gating
        // here on IsManagedQuest would silently drop the MQF lines for every
        // quest that pre-existed the current session. GetRewardLines itself
        // already returns an empty array for non-IRewardedQuest quests, so
        // calling it unconditionally is safe for vanilla quests too.
        if (mqfApi != null)
        {
            IReadOnlyList<IQuestRewardLine>? mqfLines = null;
            try { mqfLines = mqfApi.GetRewardLines(q); } catch { }
            if (mqfLines != null)
            {
                foreach (var l in mqfLines)
                {
                    lines.Add(new RewardLineRow(
                        kind: l.Kind ?? string.Empty,
                        summary: l.Summary ?? string.Empty,
                        itemId: l.ItemId,
                        npcName: l.NpcName,
                        amount: l.Amount,
                        durationDays: l.DurationDays));
                }
            }
        }

        if (lines.Count == 0)
            SynthesiseVanillaRewardLines(q, lines);

        if (lines.Count == 0)
            lines.Add(new RewardLineRow(kind: "None", summary: "(none)"));

        return lines;
    }

    private static void SynthesiseVanillaRewardLines(Quest q, List<RewardLineRow> lines)
    {
        // GetMoneyReward() is virtual on Quest; ItemDeliveryQuest overrides
        // it to fall back to `item.Price * 3` when moneyReward.Value hasn't
        // been materialised. Other subclasses (ResourceCollection, Fishing,
        // SlayMonster) don't override and instead store their payout in a
        // sibling `reward` NetInt, so we have to probe those explicitly.
        int gold = q.GetMoneyReward();
        if (gold <= 0)
            gold = ResolveSubclassReward(q);
        if (gold > 0)
            lines.Add(new RewardLineRow(kind: "Money", summary: $"{gold}g", amount: gold));

        string? desc = q.rewardDescription.Value;
        // Vanilla quest data uses "-1" as the "no reward description" sentinel
        // (see Data/Quests). Suppress it so the panel doesn't show "- -1".
        if (!string.IsNullOrEmpty(desc) && desc != "-1")
            lines.Add(new RewardLineRow(kind: "Custom", summary: desc!));

        string? friend = ResolveVanillaFriendshipTarget(q);
        if (!string.IsNullOrEmpty(friend))
            lines.Add(new RewardLineRow(
                kind: "Friendship",
                summary: $"+250 friendship with {friend}",
                npcName: friend,
                amount: 250));
    }

    private static int ResolveSubclassReward(Quest q) => q switch
    {
        ResourceCollectionQuest rcq => rcq.reward.Value,
        FishingQuest fq => fq.reward.Value,
        SlayMonsterQuest smq => smq.reward.Value,
        _ => 0
    };

    private static string? ResolveVanillaFriendshipTarget(Quest q) => q switch
    {
        ItemDeliveryQuest idq when !string.IsNullOrEmpty(idq.target.Value) => idq.target.Value,
        ResourceCollectionQuest rcq when !string.IsNullOrEmpty(rcq.target.Value) => rcq.target.Value,
        SlayMonsterQuest smq when !string.IsNullOrEmpty(smq.target.Value) && smq.target.Value != "null" => smq.target.Value,
        _ => null
    };

    public static string? ResolveGiverNpcName(Quest q) => q switch
    {
        ItemDeliveryQuest idq => string.IsNullOrEmpty(idq.target.Value) ? null : idq.target.Value,
        SlayMonsterQuest smq when !string.IsNullOrEmpty(smq.target.Value) && smq.target.Value != "null" => smq.target.Value,
        _ => null
    };

    public static string ResolveGiverDisplay(Quest q)
    {
        string? name = ResolveGiverNpcName(q);
        return string.IsNullOrEmpty(name) ? "Unknown" : name!;
    }

    public static string ResolveSourceDisplay(Quest q)
    {
        return q switch
        {
            ItemDeliveryQuest => "Stardew Valley",
            FishingQuest => "Stardew Valley",
            ResourceCollectionQuest => "Stardew Valley",
            SlayMonsterQuest => "Stardew Valley",
            _ => "Modded"
        };
    }
}
