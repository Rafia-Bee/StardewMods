using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Locations;

namespace MoreQuests.Framework.Cache;

/// One-day-scope cache for game-data assets the quest pipeline reads repeatedly.
/// Refresh() runs at SaveLoaded and DayStarted; entries are immutable for the rest of the day.
/// Per-asset try/catch isolates failures so one missing/broken asset cannot poison the rest.
internal sealed class GameDataCache
{
    private readonly IMonitor _monitor;

    private Dictionary<string, CropData>? _crops;
    private Dictionary<string, string>? _fish;
    private Dictionary<string, LocationData>? _locations;
    private Dictionary<string, string>? _cookingRecipes;
    private Dictionary<string, string>? _giftTastes;

    public GameDataCache(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public IReadOnlyDictionary<string, CropData> Crops => EnsureLoaded(ref _crops, "Data/Crops");
    public IReadOnlyDictionary<string, string> Fish => EnsureLoaded(ref _fish, "Data/Fish");
    public IReadOnlyDictionary<string, LocationData> Locations => EnsureLoaded(ref _locations, "Data/Locations");
    public IReadOnlyDictionary<string, string> CookingRecipes => EnsureLoaded(ref _cookingRecipes, "Data/CookingRecipes");
    public IReadOnlyDictionary<string, string> GiftTastes => EnsureLoaded(ref _giftTastes, "Data/NPCGiftTastes");

    /// Forces reload of every asset. Cheap when assets haven't changed since the game caches under the hood.
    public void Refresh()
    {
        _crops = null;
        _fish = null;
        _locations = null;
        _cookingRecipes = null;
        _giftTastes = null;
    }

    /// Targeted invalidation triggered by SMAPI content events.
    public void Invalidate(string assetName)
    {
        switch (assetName)
        {
            case "Data/Crops": _crops = null; break;
            case "Data/Fish": _fish = null; break;
            case "Data/Locations": _locations = null; break;
            case "Data/CookingRecipes": _cookingRecipes = null; break;
            case "Data/NPCGiftTastes": _giftTastes = null; break;
            default: return;
        }
    }

    private IReadOnlyDictionary<TKey, TValue> EnsureLoaded<TKey, TValue>(ref Dictionary<TKey, TValue>? field, string assetName) where TKey : notnull
    {
        if (field != null)
            return field;

        try
        {
            field = Game1.content.Load<Dictionary<TKey, TValue>>(assetName);
        }
        catch (Exception ex)
        {
            _monitor.Log($"GameDataCache: failed to load {assetName} ({ex.GetType().Name}: {ex.Message}); using empty fallback.", LogLevel.Warn);
            field = new Dictionary<TKey, TValue>();
        }

        return field;
    }
}
