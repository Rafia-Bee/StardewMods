using System.Collections.Generic;

namespace MoreQuestsFramework.State;

// Serializable mirror of a pending mail-quest posting. Only non-PreBuilt postings can
// round-trip through this DTO (custom Quest subclasses with NetFields can't).
internal sealed class StashedMailQuest
{
    public string MailKey { get; set; } = "";
    public string OwnerUniqueId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string MailBody { get; set; } = "";

    // Enums stored as int so JSON survives renames.
    public int QuestType { get; set; }
    public string CustomQuestType { get; set; } = "";
    public int Category { get; set; }
    public int Tier { get; set; }
    public string QuestGiver { get; set; } = "";
    public string ObjectiveItemId { get; set; } = "";
    public string ObjectiveItemName { get; set; } = "";
    public List<string> AlternativeObjectiveItemIds { get; set; } = new();
    public int ObjectiveItemWeight { get; set; } = 1;
    public List<int> AlternativeObjectiveItemWeights { get; set; } = new();
    public int ObjectiveQuantity { get; set; } = 1;
    public string? TargetMonster { get; set; }
    public int DeadlineDays { get; set; } = 5;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public string TargetMessage { get; set; } = "";

    // RewardCodec.Encode keeps this text-only (no polymorphic serializer).
    public List<string> EncodedRewards { get; set; } = new();

    public string EncodedConsequence { get; set; } = "";
}
