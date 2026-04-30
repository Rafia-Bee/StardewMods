using MoreQuestsFramework.Triggers;

namespace MoreQuestsFramework;

/// One generator for a row in the quest table. Declares its delivery channel via PostingKind
/// (daily board, special-orders board, mail, NPC-dialogue trigger) and its trigger via
/// `TriggerSource` (when does it fire?).
public interface IQuestDefinition
{
    string Id { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }

    /// UniqueID of the mod that registered this definition. Used by the framework's
    /// public API to attribute `QuestAccepted` / `QuestCompleted` events to the right
    /// owner. Empty string is treated as "framework-owned".
    string OwnerUniqueId => "";

    /// What event causes this definition to attempt to fire today. Default `DailyBoard`
    /// keeps every legacy registration on the existing weighted-pool path.
    TriggerSource Source => TriggerSource.DailyBoard;

    /// Trigger-specific options (period, date, building type, mail flag, etc). Always
    /// non-null; defaults to `TriggerInfo.Default` when the source needs no options.
    TriggerInfo Trigger => TriggerInfo.Default;

    /// Default selection weight for the daily-board pool. 0 disables. Higher = more likely.
    /// Treated as a relative weight, not a percentage.
    int DefaultWeight { get; }

    /// Hard cap on copies of this definition that can appear in one day's batch.
    int MaxPerDay { get; }

    /// Minimum days between successive postings of this definition. 0 = no cooldown.
    int CooldownDays { get; }

    /// Cheap pre-check: should this definition even be considered today?
    bool IsAvailable(QuestContext ctx);

    /// Build the concrete posting. Return null if generation failed (e.g. no matching items found).
    QuestPosting? Build(QuestContext ctx);
}
