namespace QuestJournal.Api;

// Basic info about a quest from the MoreQuests API, plus the category and posting type enums.
public interface IQuestInfo
{
    string Id { get; }
    string OwnerUniqueId { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }
}

public enum QuestCategory
{
    Animal,
    Cooking,
    Farming,
    Festival,
    Fishing,
    Foraging,
    Mining,
    Seasonal,
    Social
}

public enum PostingKind
{
    DailyBoard,
    SpecialOrder,
    Mail,
    NpcDialogue
}
