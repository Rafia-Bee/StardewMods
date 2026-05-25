namespace QuestJournal.Api;

// Local duck-typed mirror of MoreQuestsFramework's QuestInfo. SMAPI's
// proxy resolves property names against the real class. Only the slice
// the journal actually reads is declared; MQF's enum-typed fields
// (Category, Kind, Source, EffectiveSource) are intentionally omitted
// since they don't proxy cleanly across mod boundaries.
public interface IQuestInfo
{
    string Id { get; }
    string OwnerUniqueId { get; }
}
