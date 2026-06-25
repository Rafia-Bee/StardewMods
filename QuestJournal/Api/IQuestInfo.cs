namespace QuestJournal.Api;

// Basic info about a quest from the MoreQuests API. Category is a free-form string id now
// (the framework moved it off a fixed enum), so it matches whatever the quest set.
public interface IQuestInfo
{
    string Id { get; }
    string OwnerUniqueId { get; }
    string Category { get; }
    PostingKind Kind { get; }
}

public enum PostingKind
{
    DailyBoard,
    SpecialOrder,
    Mail,
    NpcDialogue
}
