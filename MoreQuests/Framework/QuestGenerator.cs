using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// <summary>
/// Generates individual quests based on category, difficulty tier, and season.
/// Picks objectives dynamically from game data including modded items.
/// </summary>
internal sealed class QuestGenerator
{
    private readonly IMonitor _monitor;
    private readonly ItemResolver _itemResolver;
    private readonly DifficultyScaler _difficultyScaler;
    private readonly AntiRepetitionTracker _antiRepetition;

    public QuestGenerator(
        IMonitor monitor,
        ItemResolver itemResolver,
        DifficultyScaler difficultyScaler,
        AntiRepetitionTracker antiRepetition)
    {
        _monitor = monitor;
        _itemResolver = itemResolver;
        _difficultyScaler = difficultyScaler;
        _antiRepetition = antiRepetition;
    }

    public GeneratedQuest? Generate(QuestCategory category, string season)
    {
        var tier = _difficultyScaler.GetTier(category);

        return category switch
        {
            QuestCategory.Farming => GenerateFarmingQuest(season, tier),
            QuestCategory.Fishing => GenerateFishingQuest(season, tier),
            QuestCategory.Mining => GenerateMiningQuest(tier),
            QuestCategory.Combat => GenerateCombatQuest(tier),
            QuestCategory.Foraging => GenerateForagingQuest(season, tier),
            QuestCategory.Cooking => GenerateCookingQuest(tier),
            QuestCategory.Social => GenerateSocialQuest(),
            _ => null
        };
    }

    private GeneratedQuest? GenerateFarmingQuest(string season, DifficultyTier tier)
    {
        var crops = _itemResolver.GetSeasonalCrops(season);
        if (crops.Count == 0)
            return null;

        // Filter out recently used objectives
        crops = crops.Where(c => !_antiRepetition.WasRecentlyUsed(c.QualifiedItemId)).ToList();
        if (crops.Count == 0)
            crops = _itemResolver.GetSeasonalCrops(season); // reset if all filtered

        // Weight by sell price (rarer crops more likely at higher tiers)
        var crop = PickWeightedItem(crops, tier);
        if (crop == null)
            return null;

        int baseQuantity = tier switch
        {
            DifficultyTier.Beginner => Game1.random.Next(3, 8),
            DifficultyTier.Intermediate => Game1.random.Next(8, 16),
            DifficultyTier.Advanced => Game1.random.Next(10, 25),
            DifficultyTier.Expert => Game1.random.Next(20, 50),
            _ => 5
        };

        int minQuality = _difficultyScaler.GetMinQuality(tier);
        int baseGold = crop.SellPrice * baseQuantity;
        int reward = _difficultyScaler.ScaleRewardGold(Math.Max(baseGold / 2, 150), tier);

        var npc = PickQuestGiver(QuestCategory.Farming);

        return new GeneratedQuest
        {
            Category = QuestCategory.Farming,
            Tier = tier,
            QuestGiverNpc = npc,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = baseQuantity,
            MinQuality = minQuality,
            GoldReward = reward,
            FriendshipReward = tier >= DifficultyTier.Intermediate ? 100 : 0,
            FriendshipRewardNpc = npc
        };
    }

    private GeneratedQuest? GenerateFishingQuest(string season, DifficultyTier tier)
    {
        var fish = _itemResolver.GetSeasonalFish(season);
        if (fish.Count == 0)
            return null;

        fish = fish.Where(f => !_antiRepetition.WasRecentlyUsed(f.QualifiedItemId)).ToList();
        if (fish.Count == 0)
            fish = _itemResolver.GetSeasonalFish(season);

        // Filter by difficulty matching tier
        var filtered = tier switch
        {
            DifficultyTier.Beginner => fish.Where(f => f.Difficulty <= 50).ToList(),
            DifficultyTier.Intermediate => fish.Where(f => f.Difficulty <= 75).ToList(),
            DifficultyTier.Advanced => fish.Where(f => f.Difficulty <= 90).ToList(),
            _ => fish
        };
        if (filtered.Count == 0)
            filtered = fish;

        var target = filtered[Game1.random.Next(filtered.Count)];

        int quantity = tier switch
        {
            DifficultyTier.Beginner => Game1.random.Next(1, 4),
            DifficultyTier.Intermediate => Game1.random.Next(3, 8),
            DifficultyTier.Advanced => Game1.random.Next(5, 15),
            DifficultyTier.Expert => Game1.random.Next(10, 30),
            _ => 3
        };

        int reward = _difficultyScaler.ScaleRewardGold(Math.Max(target.SellPrice * quantity / 2, 100), tier);
        var consequences = BuildFishingConsequences(quantity, tier);

        return new GeneratedQuest
        {
            Category = QuestCategory.Fishing,
            Tier = tier,
            QuestGiverNpc = "Willy",
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = quantity,
            GoldReward = reward,
            Consequences = consequences
        };
    }

    private GeneratedQuest? GenerateMiningQuest(DifficultyTier tier)
    {
        // TODO: implement mining/resource quest generation
        return null;
    }

    private GeneratedQuest? GenerateCombatQuest(DifficultyTier tier)
    {
        // TODO: implement combat/slaying quest generation
        return null;
    }

    private GeneratedQuest? GenerateForagingQuest(string season, DifficultyTier tier)
    {
        // TODO: implement foraging quest generation
        return null;
    }

    private GeneratedQuest? GenerateCookingQuest(DifficultyTier tier)
    {
        var recipes = _itemResolver.GetKnownRecipes();
        if (recipes.Count == 0)
            return null;

        var recipe = recipes[Game1.random.Next(recipes.Count)];

        // Build consequence from NPC gift tastes for the output dish
        var consequences = BuildCookingConsequences(recipe.OutputItem.QualifiedItemId);

        return new GeneratedQuest
        {
            Category = QuestCategory.Cooking,
            Tier = tier,
            QuestGiverNpc = "Gus",
            ObjectiveItemId = recipe.OutputItem.QualifiedItemId,
            ObjectiveItemName = recipe.OutputItem.DisplayName,
            ObjectiveQuantity = 1,
            GoldReward = _difficultyScaler.ScaleRewardGold(300, tier),
            FriendshipReward = 100,
            FriendshipRewardNpc = "Gus",
            Consequences = consequences,
            RecipeIngredients = recipe.Ingredients
        };
    }

    private GeneratedQuest? GenerateSocialQuest()
    {
        // TODO: implement social quest generation
        return null;
    }

    private List<QuestConsequence> BuildFishingConsequences(int quantity, DifficultyTier tier)
    {
        var consequences = new List<QuestConsequence>();

        if (!ModEntry.Config.ConsequencesEnabled)
            return consequences;

        if (quantity >= 30)
        {
            foreach (var npc in ModEntry.Config.EcologyMindedNPCs)
            {
                consequences.Add(new QuestConsequence
                {
                    NpcName = npc,
                    FriendshipChange = -100,
                    Tier = ConsequenceTier.Significant,
                    DialogueKey = "consequence.fishing.significant"
                });
            }
        }
        else if (quantity >= 15)
        {
            foreach (var npc in ModEntry.Config.EcologyMindedNPCs)
            {
                consequences.Add(new QuestConsequence
                {
                    NpcName = npc,
                    FriendshipChange = -30,
                    Tier = ConsequenceTier.Moderate,
                    DialogueKey = "consequence.fishing.moderate"
                });
            }
        }

        return consequences;
    }

    private List<QuestConsequence> BuildCookingConsequences(string dishItemId)
    {
        var consequences = new List<QuestConsequence>();

        if (!ModEntry.Config.ConsequencesEnabled)
            return consequences;

        // Check all NPCs' gift tastes for the dish dynamically
        // This automatically includes modded NPCs
        var giftTastes = Game1.content.Load<Dictionary<string, string>>("Data/NPCGiftTastes");
        foreach (var (npcName, tasteData) in giftTastes)
        {
            if (npcName == "Universal")
                continue;

            // Check if NPC exists in current game
            var npc = Game1.getCharacterFromName(npcName);
            if (npc == null)
                continue;

            // Parse gift taste data for hate/dislike of this item
            var fields = tasteData.Split('/');
            if (fields.Length < 9)
                continue;

            string bareId = dishItemId.StartsWith("(O)") ? dishItemId[3..] : dishItemId;

            // Field 6 = hate items, Field 4 = dislike items
            bool hates = fields[6].Split(' ').Contains(bareId);
            bool dislikes = fields[4].Split(' ').Contains(bareId);
            bool loves = fields[0].Split(' ').Contains(bareId);
            bool likes = fields[2].Split(' ').Contains(bareId);

            if (hates)
            {
                consequences.Add(new QuestConsequence
                {
                    NpcName = npcName,
                    FriendshipChange = -40,
                    Tier = ConsequenceTier.Moderate,
                    DialogueKey = "consequence.cooking.hate"
                });
            }
            else if (dislikes)
            {
                consequences.Add(new QuestConsequence
                {
                    NpcName = npcName,
                    FriendshipChange = -20,
                    Tier = ConsequenceTier.Mild,
                    DialogueKey = "consequence.cooking.dislike"
                });
            }
            else if (loves)
            {
                consequences.Add(new QuestConsequence
                {
                    NpcName = npcName,
                    FriendshipChange = 30,
                    Tier = ConsequenceTier.Positive,
                    DialogueKey = "consequence.cooking.love"
                });
            }
            else if (likes)
            {
                consequences.Add(new QuestConsequence
                {
                    NpcName = npcName,
                    FriendshipChange = 15,
                    Tier = ConsequenceTier.Positive,
                    DialogueKey = "consequence.cooking.like"
                });
            }
        }

        return consequences;
    }

    private ResolvedItem? PickWeightedItem(List<ResolvedItem> items, DifficultyTier tier)
    {
        if (items.Count == 0)
            return null;

        // Higher tiers weight toward expensive items
        var sorted = items.OrderBy(i => i.SellPrice).ToList();
        int index = tier switch
        {
            DifficultyTier.Expert => Game1.random.Next(sorted.Count / 2, sorted.Count),
            DifficultyTier.Advanced => Game1.random.Next(sorted.Count / 3, sorted.Count),
            _ => Game1.random.Next(sorted.Count)
        };

        return sorted[Math.Min(index, sorted.Count - 1)];
    }

    private static string PickQuestGiver(QuestCategory category)
    {
        var givers = category switch
        {
            QuestCategory.Farming => new[] { "Pierre", "Caroline", "Evelyn", "Lewis" },
            QuestCategory.Fishing => new[] { "Willy" },
            QuestCategory.Mining => new[] { "Clint" },
            QuestCategory.Combat => new[] { "Marlon" },
            QuestCategory.Foraging => new[] { "Linus", "Leah", "Demetrius" },
            QuestCategory.Cooking => new[] { "Gus" },
            QuestCategory.Social => new[] { "Emily", "Evelyn", "Haley" },
            _ => new[] { "Lewis" }
        };

        return givers[Game1.random.Next(givers.Length)];
    }
}
