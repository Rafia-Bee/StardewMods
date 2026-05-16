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

    bool IsAvailable(QuestContext ctx);

    // Returns null if generation failed (e.g. no matching items found).
    QuestPosting? Build(QuestContext ctx);
}
