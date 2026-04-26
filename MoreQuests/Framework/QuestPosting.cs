using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuests.Framework;

/// How a quest reaches the player.
internal enum PostingKind
{
    /// Posted on the help-wanted board. Multiple per day.
    DailyBoard,
    /// Posted on the special orders board (the second tab). Multi-objective, longer windows.
    SpecialOrder,
    /// Sent as a mail letter; accepting auto-adds the quest to the journal.
    Mail,
    /// Triggered when the farmer next speaks with the quest giver.
    NpcDialogue
}

internal enum BoardQuestType
{
    ItemDelivery,
    ResourceCollection,
    Fishing,
    SlayMonster,
    Socialize,
    Custom
}

/// Single concrete quest ready to be delivered to the player via the chosen PostingKind.
internal sealed class QuestPosting
{
    public string DefinitionId { get; set; } = "";
    public QuestCategory Category { get; set; }
    public DifficultyTier Tier { get; set; }
    public PostingKind Kind { get; set; } = PostingKind.DailyBoard;
    public BoardQuestType QuestType { get; set; }
    public string QuestGiver { get; set; } = "";

    public string ObjectiveItemId { get; set; } = "";
    public string ObjectiveItemName { get; set; } = "";
    public int ObjectiveQuantity { get; set; } = 1;
    public string? TargetMonster { get; set; }
    public string? TargetLocation { get; set; }
    public int MinQuality { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public string TargetMessage { get; set; } = "";
    public string? MailBody { get; set; }

    public int DeadlineDays { get; set; } = 5;
    public int GoldReward { get; set; }
    public int FriendshipReward { get; set; }
    public string? FriendshipRewardNpc { get; set; }
    public string? ItemReward { get; set; }
    public int ItemRewardCount { get; set; } = 1;

    public List<QuestConsequence> Consequences { get; set; } = new();

    /// If set, this Quest object is used directly instead of building one from the posting fields.
    /// Vanilla-quest definitions populate this so the vanilla random logic stays intact.
    public Quest? PreBuiltQuest { get; set; }
}

internal sealed class QuestConsequence
{
    public string NpcName { get; set; } = "";
    public int FriendshipChange { get; set; }
    public ConsequenceTier Tier { get; set; }
    public string DialogueKey { get; set; } = "";
}

internal enum ConsequenceTier
{
    Positive,
    Mild,
    Moderate,
    Significant
}
