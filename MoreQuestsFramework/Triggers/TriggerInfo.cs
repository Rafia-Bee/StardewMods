namespace MoreQuestsFramework.Triggers;

/// Bag of trigger-specific options carried alongside a `TriggerSource`. Every field
/// is optional; the relevant subset is interpreted by `TriggerEvaluator` based on
/// the source. `JsonQuestDefinition` builds one of these from the JSON `TriggerDef`;
/// C# definitions can build one explicitly via the constructor.
public sealed record TriggerInfo(
    int? EveryDays = null,
    string? Date = null,
    bool RepeatYearly = false,
    string? From = null,
    string? To = null,
    string? When = null,
    string? Building = null,
    int? DayDelay = null,
    string? Flag = null,
    string? Weather = null,
    string? Npc = null,
    string? StartDate = null,
    string? Duration = null,
    /// SpecialOrder cooldown-only mode chance (0..100). When `StartDate` is absent and
    /// `Weight > 0`, the order fires on a Sunday whose cooldown has elapsed with this
    /// percentage chance. Lets cooldown-only SpecialOrders blend into the natural weekly
    /// refresh rhythm without needing a hard-coded calendar date.
    int? Weight = null)
{
    public static readonly TriggerInfo Default = new();
}
