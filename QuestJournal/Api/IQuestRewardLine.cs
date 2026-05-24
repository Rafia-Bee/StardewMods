namespace QuestJournal.Api;

// Local duck-typed mirror of MoreQuestsFramework's QuestRewardLine. SMAPI
// wraps each real QuestRewardLine instance with a proxy that implements this
// interface based on matching property names, so the journal can read the
// fields without referencing MQF's assembly. Keep names aligned 1:1.
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
