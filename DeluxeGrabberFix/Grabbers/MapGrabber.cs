using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using DeluxeGrabberFix.Framework;
using StardewValley.Objects;

namespace DeluxeGrabberFix.Grabbers;

internal abstract class MapGrabber
{
    protected ModEntry Mod { get; set; }
    protected GameLocation Location { get; set; }
    protected bool UseGlobalMode { get; set; }
    protected List<KeyValuePair<Vector2, Object>> GrabberPairs { get; set; }
    protected IEnumerable<Object> Grabbers => GrabberPairs.Select(pair => pair.Value);
    protected Farmer Player => Game1.MasterPlayer;
    protected ModConfig Config => Mod.Config;

    public MapGrabber(ModEntry mod, GameLocation location)
    {
        Mod = mod;
        Location = location;
        UseGlobalMode = Mod.IsGlobalGrabActive;

        if (UseGlobalMode && Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
        {
            if (Mod.CachedDesignatedGrabbers != null && Mod.CachedDesignatedGrabbers.Count > 0)
            {
                GrabberPairs = Mod.CachedDesignatedGrabbers
                    .Where(pair => IsValidGrabber(pair.Value, pair.Key))
                    .ToList();
            }
            else
            {
                GrabberPairs = location.Objects.Pairs
                    .Where(pair => IsValidGrabber(pair.Value, pair.Key))
                    .ToList();
            }
            return;
        }

        if (UseGlobalMode
            && Config.globalGrabber == ModConfig.GlobalGrabberMode.Hover
            && ObjectIsGrabber(Game1.player.currentLocation.getObjectAtTile((int)Game1.lastCursorTile.X, (int)Game1.lastCursorTile.Y)))
        {
            var obj = Game1.player.currentLocation.getObjectAtTile((int)Game1.lastCursorTile.X, (int)Game1.lastCursorTile.Y);
            GrabberPairs = new List<KeyValuePair<Vector2, Object>>
            {
                new(Game1.lastCursorTile, obj)
            };
        }
        else
        {
            GrabberPairs = location.Objects.Pairs
                .Where(pair => IsValidGrabber(pair.Value, pair.Key))
                .ToList();
        }
    }

    protected bool TryAddItem(Item item, IEnumerable<KeyValuePair<Vector2, Object>> grabbers)
    {
        if (item == null || item.Stack < 1)
            return false;

        if (Config.IsItemExcluded(item.QualifiedItemId))
        {
            if (Config.visitMtVapiusExclusions && item.QualifiedItemId.Contains("_Node_"))
                Mod.LogInfo($"VMV exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else
                Mod.LogDebug($"Skipping excluded item {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            return false;
        }

        foreach (var grabber in grabbers)
        {
            if (IsValidGrabber(grabber.Value, grabber.Key))
            {
                item = AddItemToGrabberChest(grabber.Value, item);
                if (item == null)
                    return true;
            }
        }

        Mod.LogDebug($"Failed to add {item.Name} x{item.Stack} — all grabber chests full at {Location.Name}");
        return false;
    }

    protected bool TryAddItem(Item item)
    {
        return TryAddItem(item, GrabberPairs);
    }

    protected bool TryAddItems(IEnumerable<Item> items, IEnumerable<KeyValuePair<Vector2, Object>> grabbers)
    {
        var itemList = items.Where(i => i != null && i.Stack > 0).ToList();
        if (itemList.Count == 0)
            return false;

        bool allAdded = true;
        foreach (var item in itemList)
        {
            if (!TryAddItem(item, grabbers))
                allAdded = false;
        }
        return allAdded;
    }

    protected bool TryAddItems(IEnumerable<Item> items)
    {
        return TryAddItems(items, GrabberPairs);
    }

    protected void GainExperience(int skill, int exp)
    {
        if (Mod.Config.gainExperience && exp > 0)
            Player.gainExperience(skill, exp);
    }

    public bool CanGrab()
    {
        return GrabberPairs.Any(pair => IsValidGrabber(pair.Value, pair.Key));
    }

    public Dictionary<InventoryEntry, int> GetInventory()
    {
        var dictionary = new Dictionary<InventoryEntry, int>();
        foreach (var grabberPair in GrabberPairs)
        {
            if (!IsValidGrabber(grabberPair.Value, grabberPair.Key))
                continue;

            if (grabberPair.Value.heldObject.Value is Chest chest)
            {
                foreach (var item in chest.Items.Where(i => i != null))
                {
                    var key = new InventoryEntry(item);
                    if (dictionary.ContainsKey(key))
                        dictionary[key] += item.Stack;
                    else
                        dictionary.Add(key, item.Stack);
                }
            }
        }
        return dictionary;
    }

    public abstract bool GrabItems();

    private Item AddItemToGrabberChest(Object grabber, Item item)
    {
        if (grabber.heldObject.Value is not Chest chest)
            return item;

        // Remove any items with invalid (zero or negative) stack from the chest.
        // These cause Automate to report "produced an item with no stack size".
        for (int i = chest.Items.Count - 1; i >= 0; i--)
        {
            if (chest.Items[i] != null && chest.Items[i].Stack <= 0)
            {
                Mod.LogDebug($"Removed invalid item '{chest.Items[i].Name}' (Stack={chest.Items[i].Stack}) from grabber chest");
                chest.Items.RemoveAt(i);
            }
        }

        Item remaining = chest.addItem(item);
        if (chest.Items.Any(i => i != null))
            grabber.showNextIndex.Value = true;

        return remaining;
    }

    private bool IsValidGrabber(Object obj, Vector2 tile)
    {
        if (UseGlobalMode || Location.Objects.ContainsKey(tile))
        {
            return obj.QualifiedItemId == BigCraftableIds.AutoGrabber
                && obj.heldObject.Value != null
                && obj.heldObject.Value is Chest;
        }
        return false;
    }

    private bool ObjectIsGrabber(Object obj)
    {
        if (obj == null)
            return false;

        return obj.QualifiedItemId == BigCraftableIds.AutoGrabber
            && obj.heldObject.Value != null
            && obj.heldObject.Value is Chest;
    }
}
