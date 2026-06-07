namespace QuestJournal.Menu;

// One reward line shown in the journal (item, money, friendship, etc).
// Just holds the display values for that line.
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
