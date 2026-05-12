using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Cache;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.ItemTypeDefinitions;

namespace MoreQuestsFramework;

public sealed class ResolvedItem
{
    public string QualifiedItemId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int SellPrice { get; set; }
    public int Category { get; set; }
    public int Difficulty { get; set; }
    public string[] ContextTags { get; set; } = Array.Empty<string>();
}

public sealed class CookingRecipeInfo
{
    public string RecipeName { get; set; } = "";
    public ResolvedItem OutputItem { get; set; } = null!;
    public List<RecipeIngredient> Ingredients { get; set; } = new();
    public int IngredientComplexity => Ingredients.Sum(i => i.Count);
}

public sealed class RecipeIngredient
{
    public ResolvedItem Item { get; set; } = null!;
    public int Count { get; set; }
    /// When non-zero, the ingredient line was a vanilla category sentinel (negative id in
    /// `Data/CookingRecipes`) rather than a literal item id — `Item` then carries the
    /// per-category display name and a synthetic placeholder `QualifiedItemId` so step
    /// builders can still feed `AdventureStep.Items` with `$category:<CategoryId>`.
    public int CategoryId { get; set; }
    public bool IsCategoryToken => CategoryId != 0;
}

/// Dynamically resolves items from the game registry. Designed to surface modded crops/fish/objects automatically.
public sealed class ItemResolver
{
    private readonly IMonitor _monitor;
    private readonly GameDataCache _cache;

    public ItemResolver(IMonitor monitor, GameDataCache cache)
    {
        _monitor = monitor;
        _cache = cache;
    }

    public List<ResolvedItem> GetSeasonalCrops(string season)
    {
        var results = new List<ResolvedItem>();
        try
        {
            foreach (var (_, data) in _cache.Crops)
            {
                if (data.Seasons == null || !data.Seasons.Any(s => string.Equals(s.ToString(), season, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var item = TryResolveItem(data.HarvestItemId);
                if (item != null)
                    results.Add(item);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetSeasonalCrops: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    public List<ResolvedItem> GetSeasonalFish(string season, string? weatherFilter = null)
    {
        var results = new List<ResolvedItem>();
        try
        {
            foreach (var (fishId, rawData) in _cache.Fish)
            {
                var fields = rawData.Split('/');
                if (fields.Length < 13)
                    continue;
                if (fields[1] == "trap")
                    continue;

                var seasons = fields[6].Split(' ');
                if (!seasons.Any(s => s.Equals(season, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (weatherFilter != null && !fields[7].Contains(weatherFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var item = TryResolveItem("(O)" + fishId);
                if (item != null)
                {
                    if (int.TryParse(fields[1], out int difficulty))
                        item.Difficulty = difficulty;
                    results.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetSeasonalFish: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    /// Subset of `GetSeasonalFish` filtered to fish whose Data/Locations spawn entries
    /// match a location the player has visited at least once. Falls back to the full
    /// seasonal pool if the filter would produce nothing (so quests still post on a
    /// fresh save where the player has only been to the farm).
    public List<ResolvedItem> GetSeasonalFishInVisitedLocations(string season, string? weatherFilter = null)
    {
        var seasonal = GetSeasonalFish(season, weatherFilter);
        if (seasonal.Count == 0)
            return seasonal;

        HashSet<string>? allowed = TryGetSpawnableFishIdsForVisitedLocations(season);
        if (allowed == null || allowed.Count == 0)
            return seasonal;

        var filtered = seasonal.Where(f => allowed.Contains(f.QualifiedItemId)).ToList();
        return filtered.Count > 0 ? filtered : seasonal;
    }

    /// Boss/legendary fish for the current save. Walks `Data/Locations` and collects every
    /// `SpawnFishData.ItemId` whose entry sets `IsBossFish = true`. Picks up vanilla
    /// (Crimsonfish, Angler, Legend, Glacierfish, Mutant Carp, plus the family variants)
    /// and any modded fish that follow the same convention (Ridgeside Village's Deep Ridge
    /// Angler / Waterfall Snakehead / Sockeye Salmon, SVE / ESV / VMV equivalents). The
    /// returned items carry their `Difficulty` from `Data/Fish` when available so callers
    /// can sort or filter on it.
    public List<ResolvedItem> GetBossFish()
    {
        var results = new List<ResolvedItem>();
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, data) in _cache.Locations)
            {
                if (data.Fish == null)
                    continue;
                foreach (var spawn in data.Fish)
                {
                    if (spawn?.ItemId == null || !spawn.IsBossFish)
                        continue;
                    string qualified = ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    if (!seen.Add(qualified))
                        continue;
                    var item = TryResolveItem(qualified);
                    if (item == null)
                        continue;
                    string fishKey = qualified.StartsWith("(O)") ? qualified.Substring(3) : qualified;
                    if (_cache.Fish.TryGetValue(fishKey, out var rawData))
                    {
                        var fields = rawData.Split('/');
                        if (fields.Length >= 2 && int.TryParse(fields[1], out int difficulty))
                            item.Difficulty = difficulty;
                    }
                    results.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetBossFish: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    private HashSet<string>? TryGetSpawnableFishIdsForVisitedLocations(string season)
    {
        try
        {
            var visited = Game1.player.locationsVisited;
            if (visited == null || visited.Count == 0)
                return null;

            var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (locName, data) in _cache.Locations)
            {
                bool isDefault = locName.Equals("Default", StringComparison.OrdinalIgnoreCase);
                bool isVisited = visitedSet.Contains(locName);
                if (!isDefault && !isVisited)
                    continue;
                if (data.Fish == null)
                    continue;

                foreach (var spawn in data.Fish)
                {
                    if (spawn?.ItemId == null)
                        continue;
                    if (spawn.Season.HasValue && !string.Equals(spawn.Season.Value.ToString(), season, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string qualified = ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    ids.Add(qualified);
                }
            }
            return ids;
        }
        catch (Exception ex)
        {
            _monitor.Log($"TryGetSpawnableFishIdsForVisitedLocations: {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    /// All recipes from `Data/CookingRecipes` whose ingredient list resolves cleanly into
    /// either literal items or `$category:N` tokens (negative ids in vanilla recipes encode
    /// "any item with category N"). Unlike `GetKnownRecipes`, this ignores
    /// `Game1.player.cookingRecipes` — the saloon's menu doesn't depend on what the player
    /// already knows, so quests that pull from the saloon's dish pool need the full list.
    /// Recipes with a non-resolvable ingredient (corrupt data, bad modded entries) are
    /// silently dropped so the caller never has to check.
    public List<CookingRecipeInfo> GetAllCookingRecipes()
    {
        var results = new List<CookingRecipeInfo>();
        try
        {
            foreach (var (recipeName, rawData) in _cache.CookingRecipes)
            {
                var fields = rawData.Split('/');
                if (fields.Length < 3)
                    continue;

                var ingredients = ParseIngredientsStrict(fields[0]);
                if (ingredients == null)
                    continue;
                var outputItem = TryResolveItem("(O)" + fields[2].Split(' ')[0]);

                if (outputItem != null && ingredients.Count > 0)
                {
                    results.Add(new CookingRecipeInfo
                    {
                        RecipeName = recipeName,
                        OutputItem = outputItem,
                        Ingredients = ingredients
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetAllCookingRecipes: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    public List<CookingRecipeInfo> GetKnownRecipes()
    {
        var results = new List<CookingRecipeInfo>();
        try
        {
            foreach (var (recipeName, rawData) in _cache.CookingRecipes)
            {
                if (!Game1.player.cookingRecipes.ContainsKey(recipeName))
                    continue;

                var fields = rawData.Split('/');
                if (fields.Length < 3)
                    continue;

                var ingredients = ParseIngredients(fields[0]);
                var outputItem = TryResolveItem("(O)" + fields[2].Split(' ')[0]);

                if (outputItem != null && ingredients.Count > 0)
                {
                    results.Add(new CookingRecipeInfo
                    {
                        RecipeName = recipeName,
                        OutputItem = outputItem,
                        Ingredients = ingredients
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetKnownRecipes: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    public List<ResolvedItem> GetItemsByCategory(int category)
    {
        var results = new List<ResolvedItem>();
        try
        {
            foreach (var itemType in ItemRegistry.ItemTypes)
            {
                foreach (var id in itemType.GetAllIds())
                {
                    var qualifiedId = itemType.Identifier + id;
                    var parsed = ItemRegistry.GetData(qualifiedId);
                    if (parsed?.Category == category)
                    {
                        var item = TryResolveItem(qualifiedId);
                        if (item != null)
                            results.Add(item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetItemsByCategory: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    /// Subset of `GetForageItems` filtered to items whose Data/Locations Forage spawn
    /// entries match a location the player has visited at least once. Falls back to the
    /// full forage pool if the filter would produce nothing (so quests still post on a
    /// fresh save where the player has only been to the farm).
    public List<ResolvedItem> GetForageItemsInVisitedLocations(string? season = null)
    {
        var pool = GetForageItems(season);
        if (pool.Count == 0)
            return pool;

        HashSet<string>? allowed = TryGetSpawnableForageIdsForVisitedLocations();
        if (allowed == null || allowed.Count == 0)
            return pool;

        var filtered = pool.Where(f => allowed.Contains(f.QualifiedItemId)).ToList();
        return filtered.Count > 0 ? filtered : pool;
    }

    private HashSet<string>? TryGetSpawnableForageIdsForVisitedLocations()
    {
        try
        {
            var visited = Game1.player.locationsVisited;
            if (visited == null || visited.Count == 0)
                return null;

            var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (locName, data) in _cache.Locations)
            {
                bool isDefault = locName.Equals("Default", StringComparison.OrdinalIgnoreCase);
                bool isVisited = visitedSet.Contains(locName);
                if (!isDefault && !isVisited)
                    continue;
                if (data.Forage == null || data.Forage.Count == 0)
                    continue;

                foreach (var spawn in data.Forage)
                {
                    if (spawn?.ItemId == null)
                        continue;
                    string qualified = ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    ids.Add(qualified);
                }
            }
            return ids;
        }
        catch (Exception ex)
        {
            _monitor.Log($"TryGetSpawnableForageIdsForVisitedLocations: {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    /// Resolves every plain-id item entry under `Data/Shops[<shopId>]` into the deduped
    /// `ResolvedItem` list (qualified-id keyed). Skips entries whose `ItemId` is an
    /// `ItemQuery` token (anything with whitespace), since those need `ItemQueryResolver`
    /// + a runtime farmer context that callers may not want to build. Callers that need
    /// query-driven items (RANDOM_ITEMS, FLAVORED_ITEM, etc.) should pull those through
    /// the vanilla path; this helper is for "pull the basic stock list of shop X".
    public List<ResolvedItem> GetShopItems(string shopId)
    {
        var results = new List<ResolvedItem>();
        if (string.IsNullOrEmpty(shopId))
            return results;

        Dictionary<string, ShopData>? shops;
        try
        {
            shops = Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops");
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetShopItems({shopId}): could not load Data/Shops ({ex.Message}).", LogLevel.Warn);
            return results;
        }
        if (shops == null || !shops.TryGetValue(shopId, out var shop) || shop?.Items == null)
            return results;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in shop.Items)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                continue;
            if (entry.ItemId.IndexOf(' ') >= 0)
                continue;
            string qualified = ItemRegistry.QualifyItemId(entry.ItemId) ?? entry.ItemId;
            if (!seen.Add(qualified))
                continue;
            var resolved = TryResolveItem(qualified);
            if (resolved != null)
                results.Add(resolved);
        }
        return results;
    }

    /// Returns the union of (a) the vanilla beach forage roster (Coral, Sea Urchin, Nautilus
    /// Shell, Rainbow Shell, Clam, Cockle, Mussel, Oyster) and (b) any item whose `Data/Locations`
    /// Forage spawn entry sits on a location whose key contains `beach` (case-insensitive).
    /// The location filter is intentionally substring-based so modded beach locations are
    /// picked up automatically without an explicit allowlist (vanilla `Beach`, the festival
    /// `BeachNightMarket`, and modded keys like `Custom_BeachWest`, `EsBeach`, etc. all
    /// match). The vanilla roster is seeded directly because vanilla beach forage spawns
    /// via `Beach.SpawnObjects()` rather than `Data/Locations`, so a pure location-scan
    /// would miss every vanilla shell.
    public List<ResolvedItem> GetBeachForageItems()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "(O)393", // Coral
            "(O)397", // Sea Urchin
            "(O)392", // Nautilus Shell
            "(O)394", // Rainbow Shell
            "(O)372", // Clam
            "(O)718", // Cockle
            "(O)719", // Mussel
            "(O)723"  // Oyster
        };

        try
        {
            foreach (var (locName, data) in _cache.Locations)
            {
                if (locName.IndexOf("beach", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (data.Forage == null || data.Forage.Count == 0)
                    continue;
                foreach (var spawn in data.Forage)
                {
                    if (spawn?.ItemId == null)
                        continue;
                    string qualified = ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    ids.Add(qualified);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetBeachForageItems: {ex.Message}", LogLevel.Warn);
        }

        var results = new List<ResolvedItem>();
        foreach (var id in ids)
        {
            var item = TryResolveItem(id);
            if (item != null)
                results.Add(item);
        }
        return results;
    }

    /// Items tagged `forage_item` (and optionally `season_<season>`) in their context tags.
    /// Returns vanilla and modded forage in one list.
    public List<ResolvedItem> GetForageItems(string? season = null)
    {
        var results = new List<ResolvedItem>();
        string? seasonTag = season != null ? "season_" + season.ToLowerInvariant() : null;
        try
        {
            foreach (var itemType in ItemRegistry.ItemTypes)
            {
                if (itemType.Identifier != "(O)")
                    continue;
                foreach (var id in itemType.GetAllIds())
                {
                    var qualifiedId = itemType.Identifier + id;
                    var parsed = ItemRegistry.GetData(qualifiedId);
                    if (parsed?.RawData is not ObjectData obj)
                        continue;
                    var tags = obj.ContextTags;
                    if (tags == null || !tags.Contains("forage_item"))
                        continue;
                    if (seasonTag != null && !tags.Contains(seasonTag))
                        continue;
                    var item = TryResolveItem(qualifiedId);
                    if (item != null)
                        results.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetForageItems: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    public ResolvedItem? TryResolveItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        try
        {
            if (!itemId.StartsWith("("))
                itemId = "(O)" + itemId;

            var item = ItemRegistry.Create(itemId, 1, 0, true);
            if (item == null || item.DisplayName.Contains("Error", StringComparison.OrdinalIgnoreCase))
                return null;

            var data = ItemRegistry.GetData(itemId);
            int price = 0;
            string[] tags = Array.Empty<string>();
            if (data?.RawData is ObjectData obj)
            {
                price = obj.Price;
                tags = obj.ContextTags?.ToArray() ?? Array.Empty<string>();
            }

            return new ResolvedItem
            {
                QualifiedItemId = itemId,
                DisplayName = item.DisplayName,
                SellPrice = price,
                Category = data?.Category ?? 0,
                ContextTags = tags
            };
        }
        catch
        {
            return null;
        }
    }

    private List<RecipeIngredient> ParseIngredients(string ingredientString)
    {
        var ingredients = new List<RecipeIngredient>();
        var parts = ingredientString.Split(' ');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (!int.TryParse(parts[i + 1], out int count))
                continue;

            var item = TryResolveItem(parts[i]);
            if (item != null)
                ingredients.Add(new RecipeIngredient { Item = item, Count = count });
        }
        return ingredients;
    }

    /// Variant of `ParseIngredients` that returns null when the ingredient string can't be
    /// parsed cleanly (bad pair, unresolved literal id). Negative integer ids are kept as
    /// `CategoryId` sentinel ingredients with a per-category display name; callers feed
    /// these into `AdventureStep.Items` as `$category:<n>` tokens.
    private List<RecipeIngredient>? ParseIngredientsStrict(string ingredientString)
    {
        var ingredients = new List<RecipeIngredient>();
        var parts = ingredientString.Split(' ');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (!int.TryParse(parts[i + 1], out int count))
                return null;

            string idStr = parts[i];
            if (int.TryParse(idStr, out int idNum) && idNum < 0)
            {
                // Negative id encodes a vanilla category sentinel (e.g. -5 = egg, -6 = milk).
                string displayName = CategoryDisplayName(idNum);
                var placeholder = new ResolvedItem
                {
                    QualifiedItemId = $"$category:{idNum}",
                    DisplayName = displayName,
                    SellPrice = 0,
                    Category = idNum
                };
                ingredients.Add(new RecipeIngredient { Item = placeholder, Count = count, CategoryId = idNum });
                continue;
            }

            var item = TryResolveItem(idStr);
            if (item == null)
                return null;
            ingredients.Add(new RecipeIngredient { Item = item, Count = count });
        }
        return ingredients;
    }

    /// Player-facing names for the vanilla `Data/Objects` category constants that show up
    /// in `Data/CookingRecipes` ingredient lists. Used so a quest description can say
    /// "Bring 1 milk" instead of "Bring 1 (-6)".
    private static string CategoryDisplayName(int category) => category switch
    {
        -5 => "egg",
        -6 => "milk",
        -7 => "cooked dish",
        -25 => "cooking ingredient",
        -26 => "artisan good",
        -27 => "syrup",
        -28 => "monster loot",
        -75 => "vegetable",
        -79 => "fruit",
        -80 => "flower",
        -81 => "forage",
        -4 => "fish",
        -2 => "mineral",
        _ => "ingredient"
    };
}
