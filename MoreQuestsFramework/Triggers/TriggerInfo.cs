namespace MoreQuestsFramework.Triggers;

// Weight: SpecialOrder cooldown-only chance (0..100). When StartDate is absent and
// Weight>0, the order fires on a Sunday past cooldown with Weight% chance.
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
    int? Weight = null,
    string? Custom = null,
    string? DialogueText = null,
    string? CustomBoardId = null)
{
    public static readonly TriggerInfo Default = new();
}
