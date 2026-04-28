using System;
using MoreQuestsFramework.Quests;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting;

/// Builds a concrete `Quest` instance from a `QuestPosting`. Reusable factory for the
/// engine's built-in `BoardQuestType` switch. Custom Quest subclasses (e.g. content-mod
/// `Socialize`-style quests) bypass this factory by setting `posting.PreBuiltQuest`.
public static class QuestFactory
{
    /// Mints a runtime quest ID prefixed with the framework's UniqueID. MH Quest Manager
    /// uses this prefix to attribute quests back to their owning mod.
    public const string IdPrefix = "RafiaBee.MoreQuestsFramework";

    /// Builds a vanilla `Quest` (or our framework Quest subclass) from a posting.
    /// Returns null when the posting's QuestType isn't recognized.
    public static Quest? Build(QuestPosting p)
    {
        // Vanilla ItemDeliveryQuest / FishingQuest compare against `item.QualifiedItemId`,
        // so ItemId must be the qualified form (e.g. "(O)334"). Stripping the prefix
        // breaks completion for both vanilla and modded items.
        string itemId = ItemRegistry.QualifyItemId(p.ObjectiveItemId) ?? p.ObjectiveItemId;
        string giver = string.IsNullOrEmpty(p.QuestGiver) ? "Lewis" : p.QuestGiver;

        Quest? quest = p.QuestType switch
        {
            BoardQuestType.ItemDelivery or BoardQuestType.ResourceCollection => BuildItemDelivery(p, giver, itemId),
            BoardQuestType.Fishing => new MoreQuestsFishingQuest
            {
                target = { Value = giver },
                ItemId = { Value = itemId },
                numberToFish = { Value = Math.Max(1, p.ObjectiveQuantity) },
                reward = { Value = p.GoldReward },
                targetMessage = p.TargetMessage
            },
            BoardQuestType.SlayMonster => new SlayMonsterQuest
            {
                target = { Value = giver },
                monsterName = { Value = string.IsNullOrEmpty(p.TargetMonster) ? p.ObjectiveItemName : p.TargetMonster },
                numberToKill = { Value = Math.Max(1, p.ObjectiveQuantity) },
                reward = { Value = p.GoldReward },
                targetMessage = p.TargetMessage
            },
            BoardQuestType.Socialize => new ItemDeliveryQuest
            {
                target = { Value = giver },
                ItemId = { Value = itemId },
                number = { Value = 1 },
                targetMessage = p.TargetMessage
            },
            _ => null
        };

        if (quest != null)
            quest.id.Value = $"{IdPrefix}.{p.DefinitionId}.{Guid.NewGuid():N}";
        return quest;
    }

    private static MoreQuestsItemDeliveryQuest BuildItemDelivery(QuestPosting p, string giver, string itemId)
    {
        var q = new MoreQuestsItemDeliveryQuest
        {
            target = { Value = giver },
            ItemId = { Value = itemId },
            number = { Value = Math.Max(1, p.ObjectiveQuantity) },
            targetMessage = p.TargetMessage
        };
        if (!string.IsNullOrEmpty(p.ItemReward) && p.ItemRewardCount > 0)
        {
            q.customItemReward.Value = p.ItemReward;
            q.customItemRewardCount.Value = p.ItemRewardCount;
        }
        if (p.FriendshipReward > 0 && !string.IsNullOrEmpty(p.FriendshipRewardNpc))
        {
            q.friendshipRewardNpc.Value = p.FriendshipRewardNpc;
            q.friendshipRewardPoints.Value = p.FriendshipReward;
        }
        return q;
    }
}
