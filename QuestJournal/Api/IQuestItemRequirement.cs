namespace QuestJournal.Api;

// Local duck-typed mirror of MoreQuestsFramework's QuestItemRequirement. SMAPI's
// proxy maps these against the real class by property name, so keep them aligned
// 1:1.
public interface IQuestItemRequirement
{
    string ItemId { get; }
    int Quality { get; }
    int Count { get; }
}
