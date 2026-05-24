namespace QuestJournal.Menu;

// Single itemised reward line for the detail panel. Mirrors the shape of
// MQF's QuestRewardLine but stays in QuestJournal's own type system so it
// can carry vanilla-quest synthesised rows (Money / Friendship / Custom)
// alongside MQF-derived ones. Kept immutable; rows are rebuilt whenever
// the selected quest changes.
public sealed class RewardLineRow
{
    public string Kind { get; }
    public string Summary { get; }
    public string? ItemId { get; }
    public string? NpcName { get; }
    public int Amount { get; }
    public int DurationDays { get; }

    public RewardLineRow(
        string kind,
        string summary,
        string? itemId = null,
        string? npcName = null,
        int amount = 0,
        int durationDays = 0)
    {
        Kind = kind ?? string.Empty;
        Summary = summary ?? string.Empty;
        ItemId = itemId;
        NpcName = npcName;
        Amount = amount;
        DurationDays = durationDays;
    }
}
