namespace QuestJournal.Api;

// Describes an item a quest wants from you: which item, how many, and the quality.
public interface IQuestItemRequirement
{
    string ItemId { get; }
    int Quality { get; }
    int Count { get; }
}
