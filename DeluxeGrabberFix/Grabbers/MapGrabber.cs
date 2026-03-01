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
    protected List<KeyValuePair<Vector2, Object>> GrabberPairs { get; set; }
    protected IEnumerable<Object> Grabbers => GrabberPairs.Select(pair => pair.Value);
    protected Farmer Player => Game1.MasterPlayer;
    protected ModConfig Config => Mod.Config;

    public MapGrabber(ModEntry mod, GameLocation location, bool DayStarted = false)
    {
        Mod = mod;
        Location = location;

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
        {
            var allLocations = Game1.locations
                .Concat(Game1.getFarm().buildings.Select(b => b.indoors.Value))
                .Where(loc => loc != null);

            GrabberPairs = new List<KeyValuePair<Vector2, Object>>();
            foreach (var loc in allLocations)
            {
                GrabberPairs.AddRange(
                    loc.Objects.Pairs
                        .Where(pair => IsValidGrabber(pair.Value, pair.Key))
                        .ToList());
            }
            return;
        }

        if (!DayStarted
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

        Mod.LogDebug($"Grabbing item {item.Name} [{item.ParentSheetIndex}] x{item.Stack}");

        bool isBigCraftable = item is Object sObj && sObj.bigCraftable.Value;
        bool isForage = item is Object fObj && fObj.isForage();
        Mod.LogDebug($"Big craftable? {isBigCraftable}, Is forage? {isForage}");

        int originalStack = item.Stack;
        foreach (var grabber in grabbers)
        {
            if (IsValidGrabber(grabber.Value, grabber.Key))
            {
                item = AddItemToGrabberChest(grabber.Value, item);
                if (item == null)
                    return true;
            }
        }
        return item.Stack < originalStack;
    }

    protected bool TryAddItem(Item item)
    {
        return TryAddItem(item, GrabberPairs);
    }

    protected bool TryAddItems(IEnumerable<Item> items, IEnumerable<KeyValuePair<Vector2, Object>> grabbers)
    {
        bool anyAdded = false;
        foreach (var item in items)
        {
            anyAdded = TryAddItem(item, grabbers) || anyAdded;
        }
        return anyAdded;
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

        Item remaining = chest.addItem(item);
        if (chest.Items.Any(i => i != null))
            grabber.showNextIndex.Value = true;

        return remaining;
    }

    private bool IsValidGrabber(Object obj, Vector2 tile)
    {
        if (Config.globalGrabber != ModConfig.GlobalGrabberMode.Off || Location.Objects.ContainsKey(tile))
        {
            return obj.ParentSheetIndex == 165
                && obj.heldObject.Value != null
                && obj.heldObject.Value is Chest;
        }
        return false;
    }

    private bool ObjectIsGrabber(Object obj)
    {
        if (obj == null)
            return false;

        return obj.ParentSheetIndex == 165
            && obj.heldObject.Value != null
            && obj.heldObject.Value is Chest;
    }
}
