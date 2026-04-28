using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework.Rewards;

/// Centralized reward application. Custom Quest subclasses (currently
/// `MoreQuestsItemDeliveryQuest`) delegate their on-complete payouts here so any
/// completion path - in-person delivery, Mail Services Mod, future custom quest
/// types - produces the same result without duplicating the inventory/friendship
/// dance per subclass.
///
/// Phase 1 deliberately keeps the surface narrow: item drop + per-NPC friendship
/// (the only reward kinds the existing built-ins use beyond gold, which vanilla
/// `Quest.moneyReward` already handles). Money/Recipe/Mail handlers land in
/// Phase 2 alongside the declarative `Reward[]` block.
internal static class RewardApplier
{
    /// Awards the per-posting bonus item (if any) and friendship boost (if any) to
    /// the active player. Safe to call on a quest with no rewards configured - it's
    /// a no-op in that case.
    public static void ApplyItemAndFriendship(string? itemId, int itemCount, string? npcName, int friendshipPoints)
    {
        if (!string.IsNullOrEmpty(itemId) && itemCount > 0)
        {
            var reward = ItemRegistry.Create(itemId, itemCount);
            if (reward != null)
            {
                if (!Game1.player.addItemToInventoryBool(reward))
                    Game1.createItemDebris(reward, Game1.player.getStandingPosition(), 2);
            }
        }

        if (friendshipPoints > 0 && !string.IsNullOrEmpty(npcName))
        {
            var rewardNpc = Game1.getCharacterFromName(npcName);
            if (rewardNpc != null)
                Game1.player.changeFriendship(friendshipPoints, rewardNpc);
        }
    }

    /// Builds the "Reward: ..." line appended to the quest description in the journal.
    /// Vanilla bakes its reward into the description text; we mirror that look so quests
    /// posted by the framework feel native.
    public static string BuildRewardSummary(QuestPosting posting, ITranslationHelper translation)
    {
        var parts = new List<string>(2);

        if (posting.GoldReward > 0)
            parts.Add(translation.Get("quest.reward.gold", new { gold = posting.GoldReward })
                .Default($"{posting.GoldReward}g").ToString());

        if (posting.FriendshipReward > 0 && !string.IsNullOrEmpty(posting.FriendshipRewardNpc))
            parts.Add(translation.Get("quest.reward.friendship",
                new { npc = posting.FriendshipRewardNpc, points = posting.FriendshipReward })
                .Default($"+{posting.FriendshipReward} friendship with {posting.FriendshipRewardNpc}").ToString());

        if (parts.Count == 0)
            return string.Empty;

        string label = translation.Get("quest.reward.label").Default("Reward").ToString();
        return $"{label}: {string.Join(", ", parts)}";
    }
}
