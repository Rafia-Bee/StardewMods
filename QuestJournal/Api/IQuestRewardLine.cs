namespace QuestJournal.Api;

// One reward line from the MoreQuests API (kind, summary, item, amount, etc).
public interface IQuestRewardLine
{
    string Kind { get; }
    string Summary { get; }
    string? ItemId { get; }
    string? NpcName { get; }
    string? Payload { get; }
    int Amount { get; }
    int DurationDays { get; }
}
