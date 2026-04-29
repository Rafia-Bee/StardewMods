using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Rewards;
using StardewValley.Quests;

namespace MoreQuestsFramework;

/// How a quest reaches the player.
public enum PostingKind
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

public enum BoardQuestType
{
    ItemDelivery,
    ResourceCollection,
    Fishing,
    SlayMonster,
    Socialize,
    Custom
}

/// Single concrete quest ready to be delivered to the player via the chosen PostingKind.
public sealed class QuestPosting
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

    /// Declarative reward block. Phase 3 entry point - quests author rewards as
    /// `RewardSpec` records (Money / Friendship / Object / Recipe / Mail) and the
    /// poster routes them into the right fields at delivery time.
    public List<RewardSpec> Rewards { get; set; } = new();

    public List<QuestConsequence> Consequences { get; set; } = new();

    /// If set, this Quest object is used directly instead of building one from the posting fields.
    /// Vanilla-quest definitions populate this so the vanilla random logic stays intact.
    public Quest? PreBuiltQuest { get; set; }

    /// Total gold across all `MoneyReward` entries. Routed into `Quest.moneyReward` at
    /// posting time so vanilla pays it on completion.
    public int TotalMoney => RewardApplier.SumMoney(Rewards);

    /// Friendship reward to the quest giver (if any). Used by anti-spam pipeline filters
    /// like `SkipFriendshipQuestsAtMaxHeart`.
    public FriendshipReward? GiverFriendshipReward =>
        Rewards.OfType<FriendshipReward>().FirstOrDefault(r => string.Equals(r.Npc, QuestGiver, System.StringComparison.OrdinalIgnoreCase));
}

public sealed class QuestConsequence
{
    public string NpcName { get; set; } = "";
    public int FriendshipChange { get; set; }
    public ConsequenceTier Tier { get; set; }
    public string DialogueKey { get; set; } = "";
}

public enum ConsequenceTier
{
    Positive,
    Mild,
    Moderate,
    Significant
}
