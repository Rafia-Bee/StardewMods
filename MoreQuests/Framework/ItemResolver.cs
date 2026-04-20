using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;

namespace MoreQuests.Framework;

/// <summary>
/// Dynamically resolves items from the game registry for quest objectives.
/// Supports modded items by scanning Data/Crops, Data/Fish, Data/Objects, etc.
/// </summary>
internal sealed class ItemResolver
{
    private readonly IMonitor _monitor;

    public ItemResolver(IMonitor monitor)
    {
        _monitor = monitor;
    }

    /// <summary>
    /// Gets all crops that grow in the given season, including modded crops.
    /// </summary>
    public List<ResolvedItem> GetSeasonalCrops(string season)
    {
        var results = new List<ResolvedItem>();

        try
        {
            var cropData = Game1.content.Load<Dictionary<string, CropData>>("Data/Crops");
            foreach (var (cropId, data) in cropData)
            {
                if (data.Seasons == null || !data.Seasons.Any(s => string.Equals(s.ToString(), season, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var harvestItemId = data.HarvestItemId;
                if (string.IsNullOrEmpty(harvestItemId))
                    continue;

                var item = TryResolveItem(harvestItemId);
                if (item != null)
                    results.Add(item);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error loading seasonal crops: {ex.Message}", LogLevel.Warn);
        }

        return results;
    }

    /// <summary>
    /// Gets all fish available in the current season and weather conditions.
    /// </summary>
    public List<ResolvedItem> GetSeasonalFish(string season)
    {
        var results = new List<ResolvedItem>();

        try
        {
            var fishData = Game1.content.Load<Dictionary<string, string>>("Data/Fish");
            foreach (var (fishId, rawData) in fishData)
            {
                var fields = rawData.Split('/');
                if (fields.Length < 7)
                    continue;

                // Field 6 has the seasons (spring summer fall winter)
                var seasons = fields[6].Split(' ');
                if (!seasons.Any(s => s.Equals(season, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var qualifiedId = "(O)" + fishId;
                var item = TryResolveItem(qualifiedId);
                if (item != null)
                {
                    // Field 1 is difficulty
                    if (int.TryParse(fields[1], out int difficulty))
                        item.Difficulty = difficulty;
                    results.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error loading seasonal fish: {ex.Message}", LogLevel.Warn);
        }

        return results;
    }

    /// <summary>
    /// Gets all cooking recipes the player knows, with their ingredients.
    /// </summary>
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
            _monitor.Log($"Error loading recipes: {ex.Message}", LogLevel.Warn);
        }

        return results;
    }

    /// <summary>
    /// Gets all items in a specific object category.
    /// </summary>
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
            _monitor.Log($"Error loading items by category: {ex.Message}", LogLevel.Warn);
        }

        return results;
    }

    public ResolvedItem? TryResolveItem(string itemId)
    {
        try
        {
            if (!itemId.StartsWith("("))
                itemId = "(O)" + itemId;

            var item = ItemRegistry.Create(itemId, 1, 0, true);
            if (item == null || item.DisplayName.Contains("Error", StringComparison.OrdinalIgnoreCase))
                return null;

            var data = ItemRegistry.GetData(itemId);
            int price = 0;
            if (data?.RawData is ObjectData obj)
                price = obj.Price;

            return new ResolvedItem
            {
                QualifiedItemId = itemId,
                DisplayName = item.DisplayName,
                SellPrice = price,
                Category = data?.Category ?? 0
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
            var itemId = parts[i];
            if (!int.TryParse(parts[i + 1], out int count))
                continue;

            var item = TryResolveItem(itemId);
            if (item != null)
            {
                ingredients.Add(new RecipeIngredient
                {
                    Item = item,
                    Count = count
                });
            }
        }

        return ingredients;
    }
}

internal sealed class ResolvedItem
{
    public string QualifiedItemId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int SellPrice { get; set; }
    public int Category { get; set; }
    public int Difficulty { get; set; }
}

internal sealed class CookingRecipeInfo
{
    public string RecipeName { get; set; } = "";
    public ResolvedItem OutputItem { get; set; } = null!;
    public List<RecipeIngredient> Ingredients { get; set; } = new();
}

internal sealed class RecipeIngredient
{
    public ResolvedItem Item { get; set; } = null!;
    public int Count { get; set; }
}
