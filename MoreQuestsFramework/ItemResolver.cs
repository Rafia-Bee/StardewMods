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
    // Non-zero when the ingredient was a vanilla category sentinel (negative id in
    // Data/CookingRecipes); Item then carries a synthetic $category:N placeholder.
    public int CategoryId { get; set; }
    public bool IsCategoryToken => CategoryId != 0;
}

public sealed class ItemResolver
{
    private readonly IMonitor _monitor;
    private readonly GameDataCache _cache;

    public ItemResolver(IMonitor monitor, GameDataCache cache)
    {
        _monitor = monitor;
        _cache = cache;
    }

    // Optional content-supplied exclusion for quest *objective* pools. When set, the crop
    // enumeration and the category (flower / gem) enumeration drop any item it flags as true.
    // Direct TryResolveItem lookups and reward pools are left alone, so a flagged item can
    // still be handed to the player as a reward. The content mod uses this to keep rare or
    // endgame items (Qi crops, Prismatic Shard, etc.) out of "go fetch me X" requests.
    public static Func<ResolvedItem, bool>? QuestPoolExclusion { get; set; }

    // Optional content-supplied exclusion for item-delivery / shipping REQUESTS, keyed by
    // qualified item id. When set and it flags a delivery/ship quest's requested item,
    // QuestPoster drops that posting so the player is never asked to hand the item over.
    // Separate from QuestPoolExclusion (objective pools) and from the always-on reward
    // exclusion (RewardApplier.ObjectRewardExclusion); the content mod leaves this null
    // unless the player opts in. Null = off.
    public static Func<string, bool>? RequestItemExclusion { get; set; }

    private static bool IsExcludedFromQuestPool(ResolvedItem item)
    {
        var filter = QuestPoolExclusion;
        return filter != null && filter(item);
    }

    // Modded Data/Crops entries occasionally map a "seed" to non-crop harvest items
    // (e.g. Pearl, Category -4 Fish). Filter to vegetable / fruit / flower so callers
    // get the real farm-grown crops only, not pond produce piggybacking on the crop
    // pipeline.
    private static readonly HashSet<int> CropProduceCategories = new() { -75, -79, -80 };

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
                if (item == null)
                    continue;
                if (!CropProduceCategories.Contains(item.Category))
                    continue;
                if (IsExcludedFromQuestPool(item))
                    continue;
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
                if (item == null)
                    continue;
                if (IsNonFishFishEntry(item))
                    continue;
                if (!IsFishCurrentlyCatchable(item.QualifiedItemId))
                    continue;
                if (int.TryParse(fields[1], out int difficulty))
                    item.Difficulty = difficulty;
                results.Add(item);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetSeasonalFish: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    // Soft filter: a fish is considered currently uncatchable only if it has at least
    // one Data/Locations spawn entry AND every spawn entry's per-spawn Season filter
    // and Condition GSQ fail. Both `spawn.Season` (the per-spawn season enum) and
    // `spawn.Condition` (GSQ) gate spawns independently in vanilla, so honoring only
    // the latter lets fall-only legendaries like Angler slip through in winter.
    // Extended-family legendaries (Son of Crimsonfish etc., gated on
    // PLAYER_SPECIAL_ORDER_RULE_ACTIVE LEGENDARY_FAMILY) still drop out via the
    // Condition check when Qi's special order isn't active. Fish with no
    // Data/Locations spawn at all (RSV/FTM spawning) stay in since we can't tell
    // whether they're catchable.
    private bool IsFishCurrentlyCatchable(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId))
            return true;
        bool anySpawn = false;
        try
        {
            foreach (var (_, locData) in _cache.Locations)
            {
                if (locData.Fish == null)
                    continue;
                foreach (var spawn in locData.Fish)
                {
                    if (spawn?.ItemId == null) continue;
                    string qid = ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    if (!string.Equals(qid, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    anySpawn = true;
                    if (spawn.Season.HasValue && !string.Equals(spawn.Season.Value.ToString(), Game1.currentSeason, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.IsNullOrEmpty(spawn.Condition))
                        return true;
                    if (StardewValley.GameStateQuery.CheckConditions(spawn.Condition))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"IsFishCurrentlyCatchable({qualifiedItemId}): {ex.Message}", LogLevel.Warn);
            return true;
        }
        return !anySpawn;
    }

    // Drops items that live in Data/Fish but aren't really fish (SVE's Dulse Seaweed,
    // etc). SVE marks these with `fish_nonfish`; we also drop anything tagged
    // `seaweed_item` as a backstop for other mods that follow the same naming.
    private static bool IsNonFishFishEntry(ResolvedItem item)
    {
        foreach (var tag in item.ContextTags)
        {
            if (tag.Equals("fish_nonfish", StringComparison.OrdinalIgnoreCase))
                return true;
            if (tag.Equals("seaweed_item", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Falls back to the full seasonal pool if the filter would produce nothing,
    // so quests still post on a fresh save where the player has only been to the farm.
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

    // Walks Data/Locations for SpawnFishData entries with IsBossFish=true. Picks up
    // vanilla and any modded fish (RSV, SVE, VMV, etc.) following the same convention.
    // Returns every boss fish regardless of spawn Condition; callers that need only the
    // currently-catchable ones should filter further (see GetCatchableBossFish).
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

    // Vanilla extended-family legendaries. All five gate their default spawn rows on
    // Qi's LEGENDARY_FAMILY special order, but some mods (Visit Mount Vapius is the
    // known offender) re-expose the same item ids under different gates. Listing them
    // by id catches both cases. The id also doubles as the fishCaught key, so vanilla's
    // once-per-save flag is shared even when the alternate gate fires.
    private static readonly HashSet<string> ExtendedFamilyLegendaryIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "(O)898", // Son of Crimsonfish
        "(O)899", // Ms. Angler
        "(O)900", // Legend II
        "(O)901", // Glacierfish Jr.
        "(O)902"  // Radioactive Carp
    };

    // True if any clause in the GSQ condition string positively requires Qi's
    // LEGENDARY_FAMILY special order to be active. Negated clauses ("!PLAYER_..." ),
    // which the *regular* Crimsonfish / Angler / etc. spawn rows use to opt out
    // during the family order, do not count and must not be matched. Catches modded
    // analogues that follow the vanilla family pattern.
    private static bool RequiresLegendaryFamily(string? condition)
    {
        if (string.IsNullOrEmpty(condition))
            return false;
        foreach (var rawClause in condition.Split(','))
        {
            var clause = rawClause.Trim();
            if (clause.Length == 0 || clause[0] == '!')
                continue;
            if (clause.IndexOf("PLAYER_SPECIAL_ORDER_RULE_ACTIVE", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (clause.IndexOf("LEGENDARY_FAMILY", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            return true;
        }
        return false;
    }

    // Subset of GetBossFish that the player could actually catch right now. Honors
    // both `spawn.Season` (the per-spawn season enum) and the spawn entry's Condition
    // GSQ. Without the season check, fall-only Angler slips through in winter because
    // Data/Fish lists all four seasons and the only Fall restriction is on the
    // location-spawn row. Extended-family legendaries (vanilla ids 898-902 + any
    // modded analog that gates on LEGENDARY_FAMILY) are skipped outright: they're
    // only catchable while Qi's order is active, and that flag can flip off before
    // a quest deadline, leaving the player stuck on an uncatchable target.
    public List<ResolvedItem> GetCatchableBossFish()
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
                    if (ExtendedFamilyLegendaryIds.Contains(qualified))
                        continue;
                    if (RequiresLegendaryFamily(spawn.Condition))
                        continue;
                    if (spawn.Season.HasValue && !string.Equals(spawn.Season.Value.ToString(), Game1.currentSeason, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(spawn.Condition)
                        && !StardewValley.GameStateQuery.CheckConditions(spawn.Condition))
                        continue;
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
            _monitor.Log($"GetCatchableBossFish: {ex.Message}", LogLevel.Warn);
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

    // Unlike GetKnownRecipes, ignores Game1.player.cookingRecipes (the saloon's menu
    // doesn't depend on what the player knows). Recipes with unresolvable ingredients
    // are silently dropped.
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
                        if (item != null && !IsExcludedFromQuestPool(item))
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

    // Soft filter: drops items whose Data/Locations Forage entries are ALL in
    // non-visited locations. Items with no Data/Locations Forage entry anywhere
    // (e.g. RSV / VMV forage spawned by FarmTypeManager) pass through, since
    // there's no "this only spawns at X" signal to filter on.
    // Falls back to the full pool if everything would be dropped.
    public List<ResolvedItem> GetForageItemsInVisitedLocations(string? season = null)
    {
        var pool = GetForageItems(season);
        if (pool.Count == 0)
            return pool;

        var (visitedSpawns, otherSpawns) = TryGetForageSpawnSplit();
        if (visitedSpawns == null || otherSpawns == null)
            return pool;

        var filtered = pool.Where(f =>
        {
            string id = f.QualifiedItemId;
            if (visitedSpawns.Contains(id)) return true;
            return !otherSpawns.Contains(id);
        }).ToList();
        return filtered.Count > 0 ? filtered : pool;
    }

    private (HashSet<string>? visited, HashSet<string>? other) TryGetForageSpawnSplit()
    {
        try
        {
            var visited = Game1.player.locationsVisited;
            if (visited == null || visited.Count == 0)
                return (null, null);

            var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var visitedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var otherIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (locName, data) in _cache.Locations)
            {
                if (data.Forage == null || data.Forage.Count == 0)
                    continue;
                bool isVisited = locName.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    || visitedSet.Contains(locName);
                var target = isVisited ? visitedIds : otherIds;
                foreach (var spawn in data.Forage)
                {
                    if (spawn?.ItemId == null)
                        continue;
                    string qualified = ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    target.Add(qualified);
                }
            }
            return (visitedIds, otherIds);
        }
        catch (Exception ex)
        {
            _monitor.Log($"TryGetForageSpawnSplit: {ex.Message}", LogLevel.Warn);
            return (null, null);
        }
    }

    // Skips ItemQuery tokens (anything with whitespace) since those need
    // ItemQueryResolver + a runtime farmer context. Plain-id stock only.
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

    // Vanilla shells are seeded directly because Beach.SpawnObjects() drops them,
    // not Data/Locations. Location-name filter is substring-based ("beach") so modded
    // beach locations are picked up automatically.
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

    // Excludes inedible "forage" items like Nautilus Shell, Coral, Rainbow Shell
    // (all tagged forage_item but Edibility == -300). Generators that just want a
    // foraged decorative item can call GetBeachForageItems instead.
    public List<ResolvedItem> GetForageItems(string? season = null)
    {
        const int inedible = -300;
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
                    if (obj.Edibility <= 0 || obj.Edibility == inedible)
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

    // Every Data/Objects row with the tackle category. Modded tackles must use the same
    // category for the fishing rod to mount them, so the filter picks up tackles from
    // any installed content pack.
    public List<ResolvedItem> GetTackles()
    {
        var results = new List<ResolvedItem>();
        try
        {
            foreach (var itemType in ItemRegistry.ItemTypes)
            {
                if (itemType.Identifier != "(O)")
                    continue;
                foreach (var id in itemType.GetAllIds())
                {
                    string qualifiedId = itemType.Identifier + id;
                    var parsed = ItemRegistry.GetData(qualifiedId);
                    if (parsed?.RawData is not ObjectData obj)
                        continue;
                    if (obj.Category != StardewValley.Object.tackleCategory)
                        continue;
                    var item = TryResolveItem(qualifiedId);
                    if (item != null)
                        results.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetTackles: {ex.Message}", LogLevel.Warn);
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

    // Returns null on any unparseable pair or unresolved literal id. Negative ids
    // become $category:N sentinels.
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

    // Display names for the vanilla category constants in Data/CookingRecipes
    // (e.g. -6 → "milk") so quest text reads "Bring 1 milk" instead of "Bring 1 (-6)".
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
