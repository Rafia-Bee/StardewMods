namespace QuestJournal.Api;

// Local duck-typed mirror of MoreQuestsFramework's QuestInfo. SMAPI's proxy
// resolves property names against the real class. Category and Kind are enums
// on MQF's side; SMAPI maps enums across mods by member name, so we mirror the
// two enums below with the exact same member names and read them for custom-tab
// filtering. Source / EffectiveSource are omitted since the journal derives its
// own source label. Keep these member lists in sync with MQF's QuestCategory
// (Difficulty.cs) and PostingKind (QuestPosting.cs).
public interface IQuestInfo
{
    string Id { get; }
    string OwnerUniqueId { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }
}

// Mirrors MoreQuestsFramework's QuestCategory (matched by member name).
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

// Mirrors MoreQuestsFramework's PostingKind (matched by member name).
public enum PostingKind
{
    DailyBoard,
    SpecialOrder,
    Mail,
    NpcDialogue
}
