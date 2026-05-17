using MoreQuestsFramework.Triggers;

namespace MoreQuestsFramework;

public interface IQuestDefinition
{
    string Id { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }

    // UniqueID of the registering mod, used to attribute events. Empty = framework-owned.
    string OwnerUniqueId => "";

    TriggerSource Source => TriggerSource.DailyBoard;

    TriggerInfo Trigger => TriggerInfo.Default;

    // Relative weight for the daily-board pool. 0 disables.
    int DefaultWeight { get; }

    int MaxPerDay { get; }

    // 0 = no cooldown.
    int CooldownDays { get; }

    // Cheap pre-check before the pipeline spends generator time. The framework's own
    // JsonQuestDefinition currently routes through ConditionEvaluator, which reads
    // Game1.* directly rather than the ctx envelope, so ctx is more of a future seam
    // than a clean dry-run input right now. C# defs that want their own logic should
    // still read from ctx where they can so they're forward-compatible.
    // TODO (post-1.0): make ConditionEvaluator and the built-in checks route through
    // ctx so this method can honestly stand on its parameter alone.
    bool IsAvailable(QuestContext ctx);

    // Returns null if generation failed (e.g. no matching items found).
    QuestPosting? Build(QuestContext ctx);
}
