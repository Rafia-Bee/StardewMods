#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace UIInfoSuiteAddon.Framework;

internal static class GiftHelper
{
    private static readonly Dictionary<string, List<(string QualifiedId, string DisplayName)>> GiftCache = new();
    private static int _giftCacheDay = -1;

    private static HashSet<string>? _ownedItemIds;
    private static int _ownedItemsTick = -1;
    private const int OwnedCacheTicks = 120;

    internal static List<string> GetLovedGiftNames(NPC npc, int maxCount, bool excludeUniversalLoves, bool onlyOwned)
    {
        int today = Game1.Date.TotalDays;
        if (today != _giftCacheDay)
        {
            GiftCache.Clear();
            _giftCacheDay = today;
        }

        string cacheKey = $"{npc.Name}_{excludeUniversalLoves}";
        if (!GiftCache.TryGetValue(cacheKey, out var allGifts))
        {
            allGifts = ComputeLovedGifts(npc, excludeUniversalLoves);
            GiftCache[cacheKey] = allGifts;
        }

        IEnumerable<(string QualifiedId, string DisplayName)> filtered = allGifts;

        if (onlyOwned)
        {
            var owned = GetOwnedItemIds();
            filtered = allGifts.Where(g => owned.Contains(g.QualifiedId));
        }

        return filtered.Select(g => g.DisplayName).Take(maxCount).ToList();
    }

    private static List<(string QualifiedId, string DisplayName)> ComputeLovedGifts(NPC npc, bool excludeUniversalLoves)
    {
        var data = Game1.content.Load<Dictionary<string, string>>("Data/NPCGiftTastes");
        var gifts = new List<(string QualifiedId, string DisplayName)>();

        if (data.TryGetValue(npc.Name, out string? npcLine))
        {
            string[] parts = npcLine.Split('/');
            if (parts.Length >= 2)
                AddItems(parts[1], gifts);
        }

        if (!excludeUniversalLoves && data.TryGetValue("Universal_Love", out string? universalLine))
            AddItems(universalLine, gifts);

        gifts.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));
        return gifts;
    }

    private static void AddItems(string idList, List<(string QualifiedId, string DisplayName)> gifts)
    {
        foreach (string rawId in idList.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string id = rawId.Trim();
            if (string.IsNullOrEmpty(id) || id.StartsWith('-'))
                continue;

            string qualifiedId = "(O)" + id;
            var itemData = ItemRegistry.GetData(qualifiedId);
            if (itemData != null && !gifts.Any(g => g.QualifiedId == qualifiedId))
                gifts.Add((qualifiedId, itemData.DisplayName));
        }
    }

    private static HashSet<string> GetOwnedItemIds()
    {
        if (_ownedItemIds != null && Game1.ticks - _ownedItemsTick < OwnedCacheTicks)
            return _ownedItemIds;

        var owned = new HashSet<string>();

        foreach (var item in Game1.player.Items)
        {
            if (item != null)
                owned.Add(item.QualifiedItemId);
        }

        Utility.ForEachLocation(location =>
        {
            foreach (var obj in location.objects.Values)
            {
                if (obj is Chest chest && chest.playerChest.Value)
                    ScanChest(chest, owned);
            }

            Chest? fridge = location switch
            {
                FarmHouse house => house.fridge.Value,
                IslandFarmHouse house => house.fridge.Value,
                _ => null
            };
            if (fridge != null)
                ScanChest(fridge, owned);

            foreach (var building in location.buildings)
            {
                foreach (var bChest in building.buildingChests)
                    ScanChest(bChest, owned);
            }

            foreach (var furniture in location.furniture)
            {
                if (furniture is StorageFurniture dresser)
                {
                    foreach (var item in dresser.heldItems)
                    {
                        if (item != null)
                            owned.Add(item.QualifiedItemId);
                    }
                }
            }

            return true;
        });

        _ownedItemIds = owned;
        _ownedItemsTick = Game1.ticks;
        return owned;
    }

    private static void ScanChest(Chest chest, HashSet<string> owned)
    {
        foreach (var item in chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID))
        {
            if (item != null)
                owned.Add(item.QualifiedItemId);
        }
    }
}
