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
    internal IEnumerable<Object> GrabberObjects => GrabberPairs.Select(pair => pair.Value);
    protected IEnumerable<Object> Grabbers => GrabberPairs.Select(pair => pair.Value);
    protected Farmer Player => Game1.MasterPlayer;
    protected ModConfig Config => Mod.Config;

    internal GrabberType BelongsToType { get; set; } = GrabberType.All;

    public MapGrabber(ModEntry mod, GameLocation location)
    {
        Mod = mod;
        Location = location;
        UseGlobalMode = Mod.IsGlobalGrabActive;

        if (Mod.UseLocationCache && Mod.CachedGrabberPairs != null)
        {
            GrabberPairs = Mod.CachedGrabberPairs;
            return;
        }

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
        }
        else if (UseGlobalMode
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

        if (Mod.UseLocationCache)
            Mod.CachedGrabberPairs = GrabberPairs;
    }

    protected bool TryAddItem(Item item, IEnumerable<KeyValuePair<Vector2, Object>> grabbers)
    {
        if (item == null || item.Stack < 1)
            return false;

        if (Config.excludeQuestItems && item is Object obj && (obj.questItem.Value || obj.Type == "Quest"))
        {
            Mod.LogDebug($"Quest item excluded: {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            return false;
        }

        if (Config.IsItemExcluded(item.QualifiedItemId))
        {
            if (Config.visitMtVapiusExclusions && item.QualifiedItemId.Contains("_Node_"))
                Mod.LogDebug($"VMV exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else if (Config.baublesExclusions && ModConfig.BaublesExcludedItems.Contains(item.QualifiedItemId))
                Mod.LogDebug($"Baubles exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else if (Config.resourceChickensExclusions && ModConfig.ResourceChickensExcludedItems.Contains(item.QualifiedItemId))
                Mod.LogDebug($"Resource Chickens exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else if (Config.capeStardewExclusions && ModConfig.CapeStardewExcludedItems.Contains(item.QualifiedItemId))
                Mod.LogDebug($"Cape Stardew exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else
                Mod.LogDebug($"Skipping excluded item {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            return false;
        }

        var originalItem = item;
        foreach (var grabber in grabbers)
        {
            if (IsValidGrabber(grabber.Value, grabber.Key))
            {
                item = AddItemToGrabberChest(grabber.Value, item);
                if (item == null)
                {
                    Mod.Api.RaiseOnItemGrabbed(originalItem, Location);
                    return true;
                }
            }
        }

        Mod.LogDebug($"Failed to add {item.Name} x{item.Stack} — all grabber chests full at {Location.Name}");
        foreach (var grabber in grabbers)
            Mod.ReportChestFull(grabber.Value);
        return false;
    }

    protected bool TryAddItem(Item item)
    {
        return TryAddItem(item, GetFilteredGrabberPairs());
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
        return TryAddItems(items, GetFilteredGrabberPairs());
    }

    protected void GainExperience(int skill, int exp)
    {
        if (Mod.Config.gainExperience && exp > 0)
            Player.gainExperience(skill, exp);
    }

    public bool CanGrab()
    {
        return GetFilteredGrabberPairs().Any(pair => IsValidGrabber(pair.Value, pair.Key));
    }

    protected IEnumerable<KeyValuePair<Vector2, Object>> GetFilteredGrabberPairs()
    {
        if (Config.grabberMode == ModConfig.GrabberMode.Specialized && BelongsToType != GrabberType.All)
        {
            return GrabberPairs.Where(pair =>
                GrabberTypeHelper.GetGrabberType(pair.Value.QualifiedItemId) == BelongsToType);
        }
        return GrabberPairs;
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

    public void CleanupGrabberChests()
    {
        foreach (var pair in GrabberPairs)
        {
            if (pair.Value.heldObject.Value is not Chest chest)
                continue;

            for (int i = chest.Items.Count - 1; i >= 0; i--)
            {
                if (chest.Items[i] != null && chest.Items[i].Stack <= 0)
                {
                    Mod.LogDebug($"Removed invalid item '{chest.Items[i].Name}' (Stack={chest.Items[i].Stack}) from grabber chest");
                    chest.Items.RemoveAt(i);
                }
            }
        }
    }

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
        if (UseGlobalMode || Location.Objects.ContainsKey(tile))
        {
            return GrabberTypeHelper.IsGrabber(obj.QualifiedItemId)
                && obj.heldObject.Value != null
                && obj.heldObject.Value is Chest;
        }
        return false;
    }

    private bool ObjectIsGrabber(Object obj)
    {
        if (obj == null)
            return false;

        return GrabberTypeHelper.IsGrabber(obj.QualifiedItemId)
            && obj.heldObject.Value != null
            && obj.heldObject.Value is Chest;
    }
}
