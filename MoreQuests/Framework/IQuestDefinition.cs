namespace MoreQuests.Framework;

/// One generator for a row in the quest table. Declares its delivery channel via PostingKind
/// (daily board, special-orders board, mail, NPC-dialogue trigger).
internal interface IQuestDefinition
{
    string Id { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }

    /// Default selection weight for the daily-board pool. 0 disables. Higher = more likely.
    /// Treated as a relative weight, not a percentage.
    int DefaultWeight { get; }

    /// Hard cap on copies of this definition that can appear in one day's batch.
    int MaxPerDay { get; }

    /// Minimum days between successive postings of this definition. 0 = no cooldown.
    int CooldownDays { get; }

    /// Cheap pre-check: should this definition even be considered today?
    bool IsAvailable(QuestContext ctx);

    /// Build the concrete posting. Return null if generation failed (e.g. no matching items found).
    QuestPosting? Build(QuestContext ctx);
}
