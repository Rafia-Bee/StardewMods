using System;
using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Shops;

namespace MoreQuestsFramework.Rewards;

internal sealed class ShopDiscountWriter
{
    private const string AssetName = "Data/Shops";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private FrameworkState? _state;
    private bool _registered;

    public ShopDiscountWriter(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void WireState(FrameworkState state)
    {
        _state = state;
        RewardApplier.OnShopDiscountGranted = Grant;
    }

    public void Register()
    {
        if (_registered)
            return;
        _helper.Events.Content.AssetRequested += OnAssetRequested;
        _registered = true;
    }

    // Idempotent on (ShopId, AppliesTo): re-grant extends expiry instead of stacking.
    public void Grant(ShopDiscountReward reward)
    {
        if (_state == null)
        {
            _monitor.Log($"ShopDiscount '{reward.ShopId}' dropped: state not wired (no save loaded).", LogLevel.Warn);
            return;
        }
        if (string.IsNullOrEmpty(reward.ShopId) || reward.PercentOff <= 0 || reward.DurationDays <= 0)
            return;

        int today = Game1.Date?.TotalDays ?? 0;
        int expiresAfter = today + reward.DurationDays - 1;

        bool merged = false;
        foreach (var d in _state.ActiveShopDiscounts)
        {
            if (!string.Equals(d.ShopId, reward.ShopId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!SameItemFootprint(d.AppliesTo, reward.AppliesTo))
                continue;
            d.PercentOff = Math.Max(d.PercentOff, reward.PercentOff);
            d.ExpiresAfterDay = Math.Max(d.ExpiresAfterDay, expiresAfter);
            merged = true;
            break;
        }
        if (!merged)
        {
            var entry = new ActiveShopDiscount
            {
                ShopId = reward.ShopId,
                PercentOff = reward.PercentOff,
                ExpiresAfterDay = expiresAfter,
                GuaranteedStock = Math.Max(0, reward.GuaranteedStock)
            };
            if (reward.AppliesTo != null)
                entry.AppliesTo.AddRange(reward.AppliesTo);
            _state.ActiveShopDiscounts.Add(entry);
        }

        _helper.GameContent.InvalidateCache(AssetName);
        _monitor.Log(
            $"ShopDiscount granted: {reward.PercentOff}% off shop '{reward.ShopId}' until day {expiresAfter} ({reward.DurationDays}d).",
            LogLevel.Trace);
    }

    private static bool SameItemFootprint(List<string> a, List<string>? b)
    {
        int aCount = a?.Count ?? 0;
        int bCount = b?.Count ?? 0;
        if (aCount != bCount)
            return false;
        if (aCount == 0)
            return true;
        // O(n²) order-insensitive comparison; lists are small enough that allocating a
        // HashSet would be more expensive.
        foreach (var x in a!)
        {
            bool found = false;
            foreach (var y in b!)
            {
                if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (_state == null || _state.ActiveShopDiscounts.Count == 0)
            return;
        if (!e.NameWithoutLocale.IsEquivalentTo(AssetName))
            return;

        e.Edit(asset =>
        {
            var shops = asset.AsDictionary<string, ShopData>().Data;
            foreach (var disc in _state.ActiveShopDiscounts)
            {
                if (!shops.TryGetValue(disc.ShopId, out var shop) || shop?.Items == null)
                    continue;
                ApplyToShop(shop, disc);
            }
        }, AssetEditPriority.Late);
    }

    private static void ApplyToShop(ShopData shop, ActiveShopDiscount disc)
    {
        bool restrict = disc.AppliesTo.Count > 0;
        var allowed = restrict ? new HashSet<string>(disc.AppliesTo, StringComparer.OrdinalIgnoreCase) : null;

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < shop.Items.Count; i++)
        {
            var entry = shop.Items[i];
            if (entry == null) continue;
            if (restrict && !MatchesAllowed(entry.ItemId, allowed!))
                continue;
            if (restrict)
                present.Add(NormaliseItemId(entry.ItemId));

            // Price=-1 in vanilla means "use base item price"; resolve so the percent-off
            // is meaningful and vanilla's UI shows a concrete number.
            int basePrice = entry.Price;
            if (basePrice <= 0)
                basePrice = ResolveDefaultPrice(entry.ItemId);
            if (basePrice <= 0)
                continue;

            int discounted = (int)Math.Max(1, Math.Floor(basePrice * (100 - disc.PercentOff) / 100.0));
            entry.Price = discounted;
        }

        if (restrict && disc.GuaranteedStock > 0)
            InjectMissingStock(shop, disc, present);
    }

    private static void InjectMissingStock(ShopData shop, ActiveShopDiscount disc, HashSet<string> alreadyPresent)
    {
        foreach (var requested in disc.AppliesTo)
        {
            string norm = NormaliseItemId(requested);
            if (alreadyPresent.Contains(norm))
                continue;

            int basePrice = ResolveDefaultPrice(requested);
            if (basePrice <= 0)
                continue;

            int discounted = (int)Math.Max(1, Math.Floor(basePrice * (100 - disc.PercentOff) / 100.0));

            // Day suffix keeps separate quest fires distinct so an old run doesn't share
            // its purchase counter with a new one.
            string rowId = $"RafiaBee.MoreQuestsFramework.Stock.{disc.ShopId}.{norm}.{disc.ExpiresAfterDay}";

            for (int i = 0; i < shop.Items.Count; i++)
            {
                if (shop.Items[i] != null && string.Equals(shop.Items[i].Id, rowId, StringComparison.Ordinal))
                {
                    shop.Items[i].Price = discounted;
                    shop.Items[i].AvailableStock = disc.GuaranteedStock;
                    goto next;
                }
            }

            shop.Items.Add(new ShopItemData
            {
                Id = rowId,
                ItemId = requested,
                Price = discounted,
                AvailableStock = disc.GuaranteedStock
            });

        next: ;
        }
    }

    private static string NormaliseItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return string.Empty;
        if (itemId.StartsWith("(", StringComparison.Ordinal))
        {
            int closing = itemId.IndexOf(')');
            if (closing > 0)
                return itemId.Substring(closing + 1);
        }
        return itemId;
    }

    private static bool MatchesAllowed(string entryItemId, HashSet<string> allowed)
    {
        if (string.IsNullOrEmpty(entryItemId))
            return false;
        if (allowed.Contains(entryItemId))
            return true;
        // Tolerate qualified vs bare ids.
        if (entryItemId.StartsWith("(", StringComparison.Ordinal))
        {
            int closing = entryItemId.IndexOf(')');
            if (closing > 0 && allowed.Contains(entryItemId.Substring(closing + 1)))
                return true;
        }
        else if (allowed.Contains("(O)" + entryItemId))
        {
            return true;
        }
        return false;
    }

    private static int ResolveDefaultPrice(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;
        try
        {
            var data = ItemRegistry.GetData(itemId);
            // ParsedItemData has no Price; Objects expose it via RawData cast.
            if (data?.RawData is StardewValley.GameData.Objects.ObjectData obj)
                return obj.Price;
            var item = ItemRegistry.Create(itemId, 1, 0, true);
            return item?.salePrice() ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
