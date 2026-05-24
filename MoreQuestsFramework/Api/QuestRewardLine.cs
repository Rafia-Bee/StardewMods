namespace MoreQuestsFramework.Api;

// Itemised reward line for consumer-mod UIs that want to render an icon row
// instead of just BuildRewardSummary's pre-formatted text. Kind is a stable
// string id matching the underlying RewardSpec record name (without the
// "Reward" suffix), e.g. "Money", "Friendship", "Object", "Recipe", "Mail",
// "ShopDiscount", "AnimalPurchaseDiscount", "FestivalBias", "FairStarTokens",
// "Custom". Optional fields are populated only when applicable to the kind:
//   Money              -> Amount
//   Friendship         -> NpcName, Amount (points)
//   Object             -> ItemId, Amount (count)
//   Recipe             -> Payload (recipe name)
//   Mail               -> Payload (letter key)
//   ShopDiscount       -> Payload (shop id), Amount (percent off), DurationDays
//   AnimalPurchase...  -> Amount (percent off), DurationDays
//   FestivalBias       -> Payload ("luau" or "fair"), Amount (magnitude)
//   FairStarTokens     -> Amount
//   Custom             -> Payload (handler-defined string)
// Summary is the same human-readable text BuildRewardSummary would emit for
// this single line, already translated.
public sealed class QuestRewardLine
{
    public string Kind { get; }
    public string Summary { get; }
    public string? ItemId { get; }
    public string? NpcName { get; }
    public string? Payload { get; }
    public int Amount { get; }
    public int DurationDays { get; }

    public QuestRewardLine(
        string kind,
        string summary,
        string? itemId = null,
        string? npcName = null,
        string? payload = null,
        int amount = 0,
        int durationDays = 0)
    {
        Kind = kind ?? string.Empty;
        Summary = summary ?? string.Empty;
        ItemId = itemId;
        NpcName = npcName;
        Payload = payload;
        Amount = amount;
        DurationDays = durationDays;
    }
}
