using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal class InventoryEntry
{
    public readonly Item Item;
    public readonly int Quality;
    public readonly int Id;

    public string QualityName => Quality switch
    {
        1 => "Silver",
        2 => "Gold",
        4 => "Iridium",
        _ => "Normal"
    };

    public string Name => Item.Name;

    public InventoryEntry(Item item)
    {
        Item = item;
        Quality = item is Object obj ? obj.Quality : 0;
        Id = item.ParentSheetIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is InventoryEntry other && other.Id == Id && other.Quality == Quality;
    }

    public override int GetHashCode()
    {
        return (Quality << 4) ^ Id;
    }
}
