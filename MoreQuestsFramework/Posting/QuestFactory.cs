using System;
using MoreQuestsFramework.Quests;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting;

public static class QuestFactory
{
    // MH Quest Manager keys off this prefix to attribute quests back to the owning mod.
    public const string IdPrefix = "RafiaBee.MoreQuestsFramework";

    public static Quest? Build(QuestPosting p)
    {
        // Must be qualified ("(O)334"): vanilla ItemDeliveryQuest/FishingQuest compare
        // against item.QualifiedItemId, and stripping the prefix breaks completion.
        string itemId = ItemRegistry.QualifyItemId(p.ObjectiveItemId) ?? p.ObjectiveItemId;
        string giver = string.IsNullOrEmpty(p.QuestGiver) ? "Lewis" : p.QuestGiver;
        // target.Value drives both the completion gate and "deliver to" tracker overlays.
        // GiftDelivery sets DeliveryTarget to the recipient so the hand-off lands right.
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
            // Adventure quests are always PreBuiltQuest (step list lives on the subclass).
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
