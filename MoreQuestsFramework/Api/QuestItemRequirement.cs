namespace MoreQuestsFramework.Api;

// The concrete item a quest wants the player to hand in, with any "$any" /
// alternative tokens already resolved to a real eligible id. Consumer mods (the
// Quest Journal item helper) use this to spawn the right item at the right
// quality without reimplementing the framework's matching rules.
public sealed class QuestItemRequirement
{
    // Resolved, registry-valid item id (qualified where the quest stored it that way).
    public string ItemId { get; }

    // Required quality: 0 = any/normal, 1 = silver, 2 = gold, 4 = iridium.
    public int Quality { get; }

    // How many are still needed (always at least 1).
    public int Count { get; }

    public QuestItemRequirement(string itemId, int quality, int count)
    {
        ItemId = itemId;
        Quality = quality;
        Count = count;
    }
}
