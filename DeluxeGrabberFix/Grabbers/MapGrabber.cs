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
                // Audit §2.10: partition same-location grabbers ahead of cross-location
                // ones so TryAddItem prefers a same-map chest when one exists, falling
                // back to cross-map entries only when the local list is full or absent.
                GrabberPairs = Helpers.SortSameLocationFirst(
                    Mod.CachedDesignatedGrabbers.Where(pair => IsValidGrabber(pair.Value, pair.Key)),
                    IsSameLocationGrabber);
            }
            else
            {
                GrabberPairs = location.Objects.Pairs
                    .Where(pair => IsValidGrabber(pair.Value, pair.Key))
                    .ToList();
            }
        }
        else if (UseGlobalMode
            && Mod.CachedDesignatedGrabbers != null && Mod.CachedDesignatedGrabbers.Count > 0)
        {
            // Global cache populated by GrabSession (Specialized fire/sweep, or Classic
            // with a designated grabber). The All-mode case above wins this branch first;
            // we land here for the Classic-global fallback and the manual-fire paths.
            // Audit §2.10: same partition as the All branch -- cache-order alone doesn't
            // guarantee local-first routing.
            GrabberPairs = Helpers.SortSameLocationFirst(
                Mod.CachedDesignatedGrabbers.Where(pair => IsValidGrabber(pair.Value, pair.Key)),
                IsSameLocationGrabber);
        }
        else if (UseGlobalMode
            && Config.globalGrabber == ModConfig.GlobalGrabberMode.Hover)
        {
            // Hover pair was captured by GrabSession at entry (audit §2.2). Use it as the
            // sole receiver for every location's iteration so items collected from any map
            // route into the hovered chest (type-filtered by sub-grabber BelongsToType).
            // If the player wasn't hovering a grabber when the keybind fired, the pair is
            // null and we route to nothing -- preserves the pre-fix silent-no-op behavior
            // for the "fired keybind on empty space" case.
            GrabberPairs = Mod.CachedHoverGrabber.HasValue
                ? new List<KeyValuePair<Vector2, Object>> { Mod.CachedHoverGrabber.Value }
                : new List<KeyValuePair<Vector2, Object>>();
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

        if (Config.Compatibility.excludeQuestItems && item is Object obj && (obj.questItem.Value || obj.Type == "Quest"))
        {
            Mod.LogDebug($"Quest item excluded: {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            return false;
        }

        if (Config.IsItemExcluded(item.QualifiedItemId))
        {
            if (Config.Compatibility.visitMtVapiusExclusions && item.QualifiedItemId.Contains("_Node_"))
                Mod.LogDebug($"VMV exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else if (Config.Compatibility.baublesExclusions && ModConfig.BaublesExcludedItems.Contains(item.QualifiedItemId))
                Mod.LogDebug($"Baubles exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else if (Config.Compatibility.resourceChickensExclusions && ModConfig.ResourceChickensExcludedItems.Contains(item.QualifiedItemId))
                Mod.LogDebug($"Resource Chickens exclusion: skipped {item.Name} ({item.QualifiedItemId}) at {Location.Name}");
            else if (Config.Compatibility.capeStardewExclusions && ModConfig.CapeStardewExcludedItems.Contains(item.QualifiedItemId))
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
                GrabberTypeHelper.GetGrabberType(pair.Value) == BelongsToType);
        }
        return GrabberPairs;
    }

    // Audit §2.10: in global modes, the cache may contain grabbers from other maps.
    // A pair belongs to this MapGrabber's Location only if Location.Objects holds the
    // exact same Object instance at the cached tile. ReferenceEquals avoids any
    // future Object.Equals override changing this from identity to value semantics.
    protected bool IsSameLocationGrabber(KeyValuePair<Vector2, Object> pair)
    {
        return Location.Objects.TryGetValue(pair.Key, out var existing)
            && ReferenceEquals(existing, pair.Value);
    }

    // Audit §2.10: range-aware lookup that respects "local first" in global modes.
    // Same-location grabbers get the tile-distance filter; cross-location grabbers
    // skip the filter (their tile coords are in a different map's coordinate space)
    // and tail the result so they only receive items as a fallback.
    protected IEnumerable<KeyValuePair<Vector2, Object>> GetGrabbersInRangeOfTile(
        Vector2 tile, int range, ModConfig.HarvestCropsRangeMode rangeMode)
    {
        var pairs = GetFilteredGrabberPairs();
        if (!UseGlobalMode)
            return Helpers.GetNearbyObjectsToTile(tile, pairs, range, rangeMode);

        return Helpers.GetGrabbersInRangeOrCrossLocation(
            tile, pairs, range, rangeMode, IsSameLocationGrabber);
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

    public Dictionary<string, Dictionary<InventoryEntry, int>> GetPerGrabberInventory()
    {
        var result = new Dictionary<string, Dictionary<InventoryEntry, int>>();
        foreach (var grabberPair in GrabberPairs)
        {
            if (!IsValidGrabber(grabberPair.Value, grabberPair.Key))
                continue;

            string name = ModEntry.GetGrabberDisplayName(grabberPair.Value);
            if (!result.TryGetValue(name, out var inventory))
            {
                inventory = new Dictionary<InventoryEntry, int>();
                result[name] = inventory;
            }

            if (grabberPair.Value.heldObject.Value is Chest chest)
            {
                foreach (var item in chest.Items.Where(i => i != null))
                {
                    var key = new InventoryEntry(item);
                    if (inventory.ContainsKey(key))
                        inventory[key] += item.Stack;
                    else
                        inventory.Add(key, item.Stack);
                }
            }
        }
        return result;
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
        // Audit §5.4: NetField writes are observable in multiplayer (sync message
        // per write). Only flip to true when the value is actually changing, so a
        // batch add that lands many items into an already-non-empty chest doesn't
        // emit one redundant sync per add.
        if (!grabber.showNextIndex.Value && chest.Items.Any(i => i != null))
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
}
