using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;

namespace MoreQuests.Framework;

internal sealed class ResolvedItem
{
    public string QualifiedItemId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int SellPrice { get; set; }
    public int Category { get; set; }
    public int Difficulty { get; set; }
    public string[] ContextTags { get; set; } = Array.Empty<string>();
}

internal sealed class CookingRecipeInfo
{
    public string RecipeName { get; set; } = "";
    public ResolvedItem OutputItem { get; set; } = null!;
    public List<RecipeIngredient> Ingredients { get; set; } = new();
    public int IngredientComplexity => Ingredients.Sum(i => i.Count);
}

internal sealed class RecipeIngredient
{
    public ResolvedItem Item { get; set; } = null!;
    public int Count { get; set; }
}

/// Dynamically resolves items from the game registry. Designed to surface modded crops/fish/objects automatically.
internal sealed class ItemResolver
{
    private readonly IMonitor _monitor;

    public ItemResolver(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public List<ResolvedItem> GetSeasonalCrops(string season)
    {
        var results = new List<ResolvedItem>();
        try
        {
            var cropData = Game1.content.Load<Dictionary<string, CropData>>("Data/Crops");
            foreach (var (_, data) in cropData)
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
            var fishData = Game1.content.Load<Dictionary<string, string>>("Data/Fish");
            foreach (var (fishId, rawData) in fishData)
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

    private HashSet<string>? TryGetSpawnableFishIdsForVisitedLocations(string season)
    {
        try
        {
            var visited = Game1.player.locationsVisited;
            if (visited == null || visited.Count == 0)
                return null;

            var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var locationData = Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (locName, data) in locationData)
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

    public List<CookingRecipeInfo> GetKnownRecipes()
    {
        var results = new List<CookingRecipeInfo>();
        try
        {
            var recipeData = Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes");
            foreach (var (recipeName, rawData) in recipeData)
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
}
