using System.Collections.Generic;

namespace MoreQuestsFramework.Posting;

// Passed to handlers registered via IMoreQuestsModApi.RegisterCustomBoardQuestType.
// The handler returns a Quest instance for the framework to drop onto the daily
// (or mail / NpcDialogue / custom) board. ApplyPostingFields takes care of title,
// description, money, and reward encoding after the handler returns, so a handler
// only has to populate fields the vanilla Quest base doesn't already know about.
public sealed class CustomBoardQuestContext
{
    public string DefinitionId { get; }
    public string OwnerUniqueId { get; }
    public string HandlerName { get; }
    public string QuestGiver { get; }
    public string DeliveryTarget { get; }
    public string ObjectiveItemId { get; }
    public string ObjectiveItemName { get; }
    public IReadOnlyList<string> AlternativeObjectiveItemIds { get; }
    public int ObjectiveQuantity { get; }
    public int MinQuality { get; }
    public string? TargetMonster { get; }
    public string? TargetLocation { get; }
    public string TargetMessage { get; }
    public int DeadlineDays { get; }

    internal CustomBoardQuestContext(QuestPosting posting, string handlerName, string giver, string deliveryTarget)
    {
        DefinitionId = posting.DefinitionId;
        OwnerUniqueId = posting.OwnerUniqueId;
        HandlerName = handlerName;
        QuestGiver = giver;
        DeliveryTarget = deliveryTarget;
        ObjectiveItemId = posting.ObjectiveItemId;
        ObjectiveItemName = posting.ObjectiveItemName;
        AlternativeObjectiveItemIds = posting.AlternativeObjectiveItemIds.AsReadOnly();
        ObjectiveQuantity = posting.ObjectiveQuantity;
        MinQuality = posting.MinQuality;
        TargetMonster = posting.TargetMonster;
        TargetLocation = posting.TargetLocation;
        TargetMessage = posting.TargetMessage;
        DeadlineDays = posting.DeadlineDays;
    }
}
