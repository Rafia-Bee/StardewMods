namespace MoreQuests.Framework;

/// One generator for a row in the quest table. Declares its delivery channel via PostingKind
/// (daily board, special-orders board, mail, NPC-dialogue trigger).
internal interface IQuestDefinition
{
    string Id { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }

    /// Cheap pre-check: should this definition even be considered today?
    bool IsAvailable(QuestContext ctx);

    /// Build the concrete posting. Return null if generation failed (e.g. no matching items found).
    QuestPosting? Build(QuestContext ctx);
}
