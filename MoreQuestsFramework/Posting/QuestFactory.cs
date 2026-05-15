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
        // For ItemDelivery/ResourceCollection, the runtime `target.Value` drives both the
        // completion gate (`npc.Name == target.Value` in OnItemOfferedToNpc) and what MH
        // Quest Manager surfaces as "deliver to" in its overlay. Most quests have the
        // giver receiving their own item, so `DeliveryTarget` is empty and we fall back
        // to `giver`. GiftDelivery sets `DeliveryTarget` to the recipient so the hand-off
        // lands on the right villager.
        string deliveryTarget = string.IsNullOrEmpty(p.DeliveryTarget) ? giver : p.DeliveryTarget;

        Quest? quest = p.QuestType switch
        {
            BoardQuestType.ItemDelivery or BoardQuestType.ResourceCollection => BuildItemDeliveryQuest(p, itemId, deliveryTarget),
            BoardQuestType.Fishing => new MoreQuestsFishingQuest
            {
                target = { Value = giver },
                ItemId = { Value = itemId },
                numberToFish = { Value = Math.Max(1, p.ObjectiveQuantity) },
                reward = { Value = p.TotalMoney },
                catchLocationName = { Value = p.CatchLocationName ?? string.Empty },
                catchMinSize = { Value = Math.Max(0, p.CatchMinSize) },
                catchMaxSize = { Value = Math.Max(0, p.CatchMaxSize) },
                catchWeather = { Value = p.CatchWeather ?? string.Empty },
                catchAnyFish = { Value = p.CatchAnyFish },
                catchProgressTemplate = { Value = p.CatchProgressTemplate ?? string.Empty },
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
            BoardQuestType.Ship => BuildShipQuest(p, itemId, giver),
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

    private static MoreQuestsItemDeliveryQuest BuildItemDeliveryQuest(QuestPosting p, string itemId, string deliveryTarget)
    {
        var quest = new MoreQuestsItemDeliveryQuest
        {
            target = { Value = deliveryTarget },
            ItemId = { Value = itemId },
            number = { Value = Math.Max(1, p.ObjectiveQuantity) },
            minQuality = { Value = Math.Max(0, p.MinQuality) },
            targetMessage = p.TargetMessage
        };
        for (int i = 0; i < p.AlternativeObjectiveItemIds.Count; i++)
        {
            string alt = p.AlternativeObjectiveItemIds[i];
            string qualified = ItemRegistry.QualifyItemId(alt) ?? alt;
            quest.alternativeItemIds.Add(qualified);
            int qty = i < p.AlternativeObjectiveItemQuantities.Count
                ? Math.Max(0, p.AlternativeObjectiveItemQuantities[i])
                : 0;
            quest.alternativeItemQuantities.Add(qty);
        }
        return quest;
    }

    private static MoreQuestsShipQuest BuildShipQuest(QuestPosting p, string itemId, string giver)
    {
        var ship = new MoreQuestsShipQuest
        {
            target = { Value = giver },
            itemId = { Value = itemId },
            itemWeight = { Value = Math.Max(1, p.ObjectiveItemWeight) },
            numberToShip = { Value = Math.Max(1, p.ObjectiveQuantity) },
            allowDecorShipping = { Value = p.AllowDecorShipping },
            objectiveItemName = string.IsNullOrEmpty(p.ObjectiveItemName) ? itemId : p.ObjectiveItemName,
            targetMessage = p.TargetMessage
        };
        for (int i = 0; i < p.AlternativeObjectiveItemIds.Count; i++)
        {
            string alt = p.AlternativeObjectiveItemIds[i];
            string qualified = ItemRegistry.QualifyItemId(alt) ?? alt;
            ship.alternativeItemIds.Add(qualified);
            int weight = i < p.AlternativeObjectiveItemWeights.Count
                ? Math.Max(1, p.AlternativeObjectiveItemWeights[i])
                : 1;
            ship.alternativeItemWeights.Add(weight);
        }
        return ship;
    }
}
