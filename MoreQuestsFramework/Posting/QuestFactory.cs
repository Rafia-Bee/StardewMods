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
            BoardQuestType.ItemDelivery or BoardQuestType.ResourceCollection => new MoreQuestsItemDeliveryQuest
            {
                target = { Value = giver },
                ItemId = { Value = itemId },
                number = { Value = Math.Max(1, p.ObjectiveQuantity) },
                targetMessage = p.TargetMessage
            },
            BoardQuestType.Fishing => new MoreQuestsFishingQuest
            {
                target = { Value = giver },
                ItemId = { Value = itemId },
                numberToFish = { Value = Math.Max(1, p.ObjectiveQuantity) },
                reward = { Value = p.TotalMoney },
                targetMessage = p.TargetMessage
            },
            BoardQuestType.SlayMonster => new SlayMonsterQuest
            {
                target = { Value = giver },
                monsterName = { Value = string.IsNullOrEmpty(p.TargetMonster) ? p.ObjectiveItemName : p.TargetMonster },
                numberToKill = { Value = Math.Max(1, p.ObjectiveQuantity) },
                reward = { Value = p.TotalMoney },
                targetMessage = p.TargetMessage
            },
            BoardQuestType.Socialize => new ItemDeliveryQuest
            {
                target = { Value = giver },
                ItemId = { Value = itemId },
                number = { Value = 1 },
                targetMessage = p.TargetMessage
            },
            // Adventure quests are always pre-built by the JSON path / generators because
            // their step list lives on the Quest subclass itself; the factory has nothing
            // to construct from posting-level scalars alone.
            BoardQuestType.Adventure => null,
            _ => null
        };

        if (quest != null)
            quest.id.Value = $"{IdPrefix}.{p.DefinitionId}.{Guid.NewGuid():N}";
        return quest;
    }
}
