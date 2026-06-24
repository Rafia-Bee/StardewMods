using MoreQuestsFramework.Triggers;

namespace MoreQuestsFramework.Api;

// Read-only snapshot of a registered quest definition for consumer-mod UIs
// (debug menus, quest browsers, GMCM helpers). Source is the def's declared
// TriggerSource, EffectiveSource is what the pipeline actually uses after any
// OverrideTriggerSource calls; usually identical, can diverge when a mod toggles
// a quest between sources at runtime.
public sealed class QuestInfo
{
    public string Id { get; }
    public string OwnerUniqueId { get; }
    public string Category { get; }
    public PostingKind Kind { get; }
    public TriggerSource Source { get; }
    public TriggerSource EffectiveSource { get; }

    public QuestInfo(string id, string ownerUniqueId, string category, PostingKind kind, TriggerSource source, TriggerSource effectiveSource)
    {
        Id = id;
        OwnerUniqueId = ownerUniqueId;
        Category = category;
        Kind = kind;
        Source = source;
        EffectiveSource = effectiveSource;
    }
}
