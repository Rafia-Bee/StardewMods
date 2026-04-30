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
    string? Npc = null)
{
    public static readonly TriggerInfo Default = new();
}
