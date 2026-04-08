using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal class InventoryEntry
{
    public readonly Item Item;
    public readonly int Quality;
    public readonly string QualifiedItemId;

    public string QualityName => Quality switch
    {
        1 => "Silver",
        2 => "Gold",
        4 => "Iridium",
        _ => "Normal"
    };

    public string QualityKey => Quality switch
    {
        1 => "log.quality-silver",
        2 => "log.quality-gold",
        4 => "log.quality-iridium",
        _ => "log.quality-normal"
    };

    public string Name => Item.Name;

    public string DisplayName => Item.DisplayName;

    public InventoryEntry(Item item)
    {
        Item = item;
        Quality = item is Object obj ? obj.Quality : 0;
        QualifiedItemId = item.QualifiedItemId;
    }

    public override bool Equals(object obj)
    {
        return obj is InventoryEntry other && other.QualifiedItemId == QualifiedItemId && other.Quality == Quality;
    }

    public override int GetHashCode()
    {
        return (Quality << 4) ^ (QualifiedItemId?.GetHashCode() ?? 0);
    }
}
