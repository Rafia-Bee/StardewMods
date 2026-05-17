using System;
using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.FarmAnimals;

namespace MoreQuestsFramework.Rewards;

// Asset-edit approach so third-party menu rewrites (Livestock Bazaar etc) pick up
// the discount without a per-menu patch.
internal sealed class AnimalPurchaseDiscountWriter
{
    private const string AssetName = "Data/FarmAnimals";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private FrameworkState? _state;
    private bool _registered;

    public AnimalPurchaseDiscountWriter(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void WireState(FrameworkState state)
    {
        _state = state;
        RewardApplier.OnAnimalPurchaseDiscountGranted = Grant;
    }

    public void Register()
    {
        if (_registered)
            return;
        _helper.Events.Content.AssetRequested += OnAssetRequested;
        _registered = true;
    }

    public void Grant(AnimalPurchaseDiscountReward reward)
    {
        if (_state == null)
        {
            _monitor.Log("AnimalPurchaseDiscount dropped: state not wired (no save loaded).", LogLevel.Warn);
            return;
        }
        if (reward.PercentOff <= 0 || reward.DurationDays <= 0)
            return;

        int today = Game1.Date?.TotalDays ?? 0;
        int expiresAfter = today + reward.DurationDays - 1;

        if (_state.ActiveAnimalPurchaseDiscounts.Count > 0)
        {
            var existing = _state.ActiveAnimalPurchaseDiscounts[0];
            existing.PercentOff = Math.Max(existing.PercentOff, reward.PercentOff);
            existing.ExpiresAfterDay = Math.Max(existing.ExpiresAfterDay, expiresAfter);
        }
        else
        {
            _state.ActiveAnimalPurchaseDiscounts.Add(new ActiveAnimalPurchaseDiscount
            {
                PercentOff = reward.PercentOff,
                ExpiresAfterDay = expiresAfter
            });
        }

        _helper.GameContent.InvalidateCache(AssetName);
        _monitor.Log(
            $"AnimalPurchaseDiscount granted: {reward.PercentOff}% off animal purchases until day {expiresAfter} ({reward.DurationDays}d).",
            LogLevel.Trace);
    }

    public void SweepExpired()
    {
        if (_state == null || _state.ActiveAnimalPurchaseDiscounts.Count == 0)
            return;

        int today = Game1.Date?.TotalDays ?? 0;
        bool removed = false;
        for (int i = _state.ActiveAnimalPurchaseDiscounts.Count - 1; i >= 0; i--)
        {
            if (today > _state.ActiveAnimalPurchaseDiscounts[i].ExpiresAfterDay)
            {
                _state.ActiveAnimalPurchaseDiscounts.RemoveAt(i);
                removed = true;
            }
        }
        if (removed)
            _helper.GameContent.InvalidateCache(AssetName);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (_state == null || _state.ActiveAnimalPurchaseDiscounts.Count == 0)
            return;
        if (!e.NameWithoutLocale.IsEquivalentTo(AssetName))
            return;

        e.Edit(asset =>
        {
            var data = asset.AsDictionary<string, FarmAnimalData>().Data;
            int totalPercent = ResolveBestPercent();
            if (totalPercent <= 0)
                return;
            foreach (var pair in data)
            {
                var entry = pair.Value;
                if (entry == null || entry.PurchasePrice <= 0)
                    continue;
                int discounted = (int)Math.Max(1, Math.Floor(entry.PurchasePrice * (100 - totalPercent) / 100.0));
                entry.PurchasePrice = discounted;
            }
        }, AssetEditPriority.Late);
    }

    private int ResolveBestPercent()
    {
        int best = 0;
        foreach (var d in _state!.ActiveAnimalPurchaseDiscounts)
            if (d.PercentOff > best)
                best = d.PercentOff;
        return Math.Min(95, best);
    }
}
