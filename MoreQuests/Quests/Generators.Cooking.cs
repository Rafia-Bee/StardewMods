using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Quests;

internal static partial class Generators
{
    private static QuestPosting? CravingDishGenerator(QuestContext ctx)
    {
        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;

        var tastes = ctx.Data.GiftTastes;
        var allRecipes = ctx.Data.CookingRecipes;

        var knownRecipes = ctx.Items.GetKnownRecipes();
        if (knownRecipes.Count == 0)
            return null;

        var candidates = new List<(string Giver, CookingRecipeInfo Dish, HashSet<string> Loved, HashSet<string> Liked, HashSet<string> Neutral)>();
        foreach (var giver in npcs)
        {
            if (!tastes.TryGetValue(giver, out var tasteData))
                continue;

            var fields = tasteData.Split('/');
            if (fields.Length < 10)
                continue;

            var loved = fields[1].Split(' ').ToHashSet();
            var liked = fields[3].Split(' ').ToHashSet();
            var neutral = fields[9].Split(' ').ToHashSet();

            foreach (var r in knownRecipes)
            {
                string bare = StripPrefix(r.OutputItem.QualifiedItemId);
                if (loved.Contains(bare) || liked.Contains(bare) || neutral.Contains(bare))
                    candidates.Add((giver, r, loved, liked, neutral));
            }
        }

        if (candidates.Count == 0)
        {
            ctx.Monitor.Log($"CravingDish: no NPC/recipe match across {npcs.Count} met NPCs and {knownRecipes.Count} known recipes.", LogLevel.Trace);
            return null;
        }

        var pick = candidates[Game1.random.Next(candidates.Count)];
        string requestedBareId = StripPrefix(pick.Dish.OutputItem.QualifiedItemId);
        var rewardDish = PickRewardDish(allRecipes, ctx.Items, pick.Loved, pick.Liked, requestedBareId);

        var posting = new QuestPosting
        {
            Category = QuestCategory.Cooking,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = pick.Giver,
            ObjectiveItemId = pick.Dish.OutputItem.QualifiedItemId,
            ObjectiveItemName = pick.Dish.OutputItem.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(pick.Giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.cooking.craving.title", new { npc = pick.Giver }),
            Description = ModEntry.I18n.Get("quest.cooking.craving.description", new { npc = pick.Giver, item = pick.Dish.OutputItem.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.cooking.craving.objective", new { item = pick.Dish.OutputItem.DisplayName, npc = pick.Giver }),
            TargetMessage = ModEntry.I18n.Get("quest.cooking.craving.targetMessage", new { item2 = rewardDish?.DisplayName ?? pick.Dish.OutputItem.DisplayName })
        };

        if (rewardDish != null)
            posting.Rewards.Add(new ObjectReward(rewardDish.QualifiedItemId));

        return posting;
    }

    private static ResolvedItem? PickRewardDish(
        IReadOnlyDictionary<string, string> allRecipes,
        ItemResolver items,
        HashSet<string> loved,
        HashSet<string> liked,
        string excludeBareId)
    {
        var candidates = new List<ResolvedItem>();
        foreach (var (_, raw) in allRecipes)
        {
            var parts = raw.Split('/');
            if (parts.Length < 3)
                continue;
            string outputBare = parts[2].Split(' ')[0];
            if (outputBare == excludeBareId)
                continue;
            if (!loved.Contains(outputBare) && !liked.Contains(outputBare))
                continue;
            var resolved = items.TryResolveItem("(O)" + outputBare);
            if (resolved != null)
                candidates.Add(resolved);
        }
        if (candidates.Count == 0)
            return null;
        return candidates[Game1.random.Next(candidates.Count)];
    }

    /// AdventureQuest where a saloon NPC asks for a small set of in-season attainable
    /// ingredients (seasonal crops, forage in visited locations, fish in visited locations,
    /// plus category tokens like any-egg/any-milk). One Deliver step per ingredient. The
    /// framework picks an overlap-best recipe for flavour text, Tier 1 GiftTastes consequence,
    /// and per-NPC FriendshipMultiSmall to saloon-crowd villagers who love/like the dish.
    /// When no recipe overlaps, the no-dish i18n fallback runs and the consequence + crowd
    /// friendship reward are skipped.
    private static QuestPosting? WeeklySpecialCommon(QuestContext ctx)
    {
        return BuildAttainableWeeklySpecial(
            ctx,
            tier: DifficultyTier.Beginner,
            consequenceTier: ConsequenceTier.Tier1,
            goldBase: ctx.Config.GoldBeginnerBase,
            deadlineKind: DeadlineKind.Medium,
            allSeasons: false,
            saloonCrowdMagnitude: ctx.Config.FriendshipMultiSmall,
            titleKey: "quest.cooking.weeklySpecial.common.title",
            descriptionKey: "quest.cooking.weeklySpecial.common.description",
            descriptionNoDishKey: "quest.cooking.weeklySpecial.common.description.noDish",
            consequenceLovedKey: "quest.cooking.weeklySpecial.common.consequence.loved",
            consequenceHatedKey: "quest.cooking.weeklySpecial.common.consequence.hated");
    }

    /// Same flow as the Common variant but the pool spans all four seasons. Gold steps up
    /// to GoldIntermediateBase, deadline to Long, consequence to Tier 2, friendship to
    /// FriendshipIntermediate. Skill gate (Cooking 4 or Farming 5) is in quests.json.
    private static QuestPosting? WeeklySpecialComplex(QuestContext ctx)
    {
        return BuildAttainableWeeklySpecial(
            ctx,
            tier: DifficultyTier.Advanced,
            consequenceTier: ConsequenceTier.Tier2,
            goldBase: ctx.Config.GoldIntermediateBase,
            deadlineKind: DeadlineKind.Long,
            allSeasons: true,
            saloonCrowdMagnitude: ctx.Config.FriendshipIntermediate,
            titleKey: "quest.cooking.weeklySpecial.complex.title",
            descriptionKey: "quest.cooking.weeklySpecial.complex.description",
            descriptionNoDishKey: "quest.cooking.weeklySpecial.complex.description.noDish",
            consequenceLovedKey: "quest.cooking.weeklySpecial.complex.consequence.loved",
            consequenceHatedKey: "quest.cooking.weeklySpecial.complex.consequence.hated");
    }

    /// Builds the attainable-ingredient WeeklySpecial. Pool = seasonal crops + visited-location
    /// forage + visited-location fish + curated cooking category tokens. Dish is the
    /// recipe with the highest ingredient overlap with the picks (random tiebreak). No-dish
    /// fallback drops the consequence + crowd-friendship; gold still pays out.
    /// Parameterised so Complex can reuse with an all-seasons pool and stronger magnitudes.
    private static QuestPosting? BuildAttainableWeeklySpecial(
        QuestContext ctx,
        DifficultyTier tier,
        ConsequenceTier consequenceTier,
        int goldBase,
        DeadlineKind deadlineKind,
        bool allSeasons,
        int saloonCrowdMagnitude,
        string titleKey,
        string descriptionKey,
        string descriptionNoDishKey,
        string consequenceLovedKey,
        string consequenceHatedKey)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        var pool = BuildAttainableIngredientPool(ctx, allSeasons);
        if (pool.Count == 0)
            return null;

        int wanted = ResolveWeeklySpecialCommonIngredientCount(ctx);
        int take = Math.Min(wanted, pool.Count);

        var available = new List<AttainableIngredient>(pool);
        var picks = new List<AttainableIngredient>(take);
        for (int i = 0; i < take; i++)
        {
            int idx = Game1.random.Next(available.Count);
            picks.Add(available[idx]);
            available.RemoveAt(idx);
        }

        var requests = new List<(AttainableIngredient Ing, int Qty)>(picks.Count);
        foreach (var p in picks)
        {
            // Category tokens cap at 2 so "Bring 5 of any milk" doesn't sandbag early-game
            // saves. Concrete in-season items get up to 3 since they're usually farmable.
            int qty = p.IsCategoryToken
                ? Game1.random.Next(1, 3)
                : Game1.random.Next(1, 4);
            requests.Add((p, qty));
        }

        var steps = new List<AdventureStepState>(requests.Count);
        foreach (var (ing, qty) in requests)
        {
            steps.Add(new AdventureStepState
            {
                Name = "Deliver_" + Sanitise(ing.DisplayName),
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { giver },
                Items = new List<string> { ing.MatcherToken },
                Count = qty,
                Description = ModEntry.I18n.Get(
                    "quest.cooking.weeklySpecial.step.deliver",
                    new { count = qty, item = ing.DisplayName, npc = giver })
            });
        }

        var dish = PickOverlapDish(ctx, picks);

        string completionDialogue = dish != null
            ? ModEntry.I18n.Get("quest.cooking.weeklySpecial.targetMessage", new { dish = dish.DisplayName })
            : ModEntry.I18n.Get("quest.cooking.weeklySpecial.common.targetMessage.noDish");

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: giver, completionDialogue: completionDialogue);

        var rewards = new List<RewardSpec> { new MoneyReward(goldBase) };
        if (dish != null)
            AddSaloonCrowdFriendship(ctx, dish.QualifiedItemId, rewards, saloonCrowdMagnitude);

        ConsequenceSpec? consequence = null;
        if (dish != null && ModEntry.Config.ConsequencesEnabled)
        {
            consequence = new ConsequenceSpec
            {
                Tier = consequenceTier,
                Source = ConsequenceSource.GiftTastes,
                Subject = dish.QualifiedItemId,
                LovedLine = ModEntry.I18n.Get(
                    consequenceLovedKey,
                    new { dish = dish.DisplayName, npc = giver }),
                HatedLine = ModEntry.I18n.Get(
                    consequenceHatedKey,
                    new { dish = dish.DisplayName, npc = giver })
            };
        }

        string ingredientsList = string.Join(", ", requests.Select(r => r.Qty + " " + r.Ing.DisplayName));

        string description = dish != null
            ? ModEntry.I18n.Get(
                descriptionKey,
                new { npc = giver, dish = dish.DisplayName, ingredients = ingredientsList })
            : ModEntry.I18n.Get(
                descriptionNoDishKey,
                new { npc = giver, ingredients = ingredientsList });

        return new QuestPosting
        {
            Category = QuestCategory.Cooking,
            Tier = tier,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(deadlineKind, ctx.Config),
            Rewards = rewards,
            Consequence = consequence,
            Title = ModEntry.I18n.Get(titleKey, new { npc = giver }),
            Description = description,
            TargetMessage = completionDialogue,
            PreBuiltQuest = quest
        };
    }

    /// One entry in the WeeklySpecial ingredient pool. Wraps either a concrete ResolvedItem
    /// or a curated category sentinel (CategoryId != 0). MatcherToken is the qualified id
    /// for literals, a `$category:N` matcher for sentinels.
    private sealed class AttainableIngredient
    {
        public string QualifiedItemId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int Category { get; init; }
        public int CategoryId { get; init; }
        public bool IsCategoryToken => CategoryId != 0;
        public string MatcherToken { get; init; } = string.Empty;
    }

    private static readonly string[] AllSeasons = { "spring", "summer", "fall", "winter" };

    private static List<AttainableIngredient> BuildAttainableIngredientPool(QuestContext ctx, bool allSeasons = false)
    {
        var result = new List<AttainableIngredient>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(AttainableIngredient ing)
        {
            if (!string.IsNullOrEmpty(ing.MatcherToken) && seen.Add(ing.MatcherToken))
                result.Add(ing);
        }

        var seasons = allSeasons ? AllSeasons : new[] { ctx.Season };
        foreach (var season in seasons)
        {
            foreach (var c in ctx.Items.GetSeasonalCrops(season))
                Add(new AttainableIngredient
                {
                    QualifiedItemId = c.QualifiedItemId,
                    DisplayName = c.DisplayName,
                    Category = c.Category,
                    MatcherToken = c.QualifiedItemId
                });
            foreach (var fi in ctx.Items.GetSeasonalFishInVisitedLocations(season))
                Add(new AttainableIngredient
                {
                    QualifiedItemId = fi.QualifiedItemId,
                    DisplayName = fi.DisplayName,
                    Category = fi.Category,
                    MatcherToken = fi.QualifiedItemId
                });
        }
        foreach (var f in ctx.Items.GetForageItemsInVisitedLocations(allSeasons ? null : ctx.Season))
            Add(new AttainableIngredient
            {
                QualifiedItemId = f.QualifiedItemId,
                DisplayName = f.DisplayName,
                Category = f.Category,
                MatcherToken = f.QualifiedItemId
            });

        var categoryTokens = new (int Cat, string I18nLeaf)[]
        {
            (-5, "egg"),
            (-6, "milk"),
            (-4, "fish"),
            (-75, "vegetable"),
            (-79, "fruit"),
            (-80, "flower"),
        };
        foreach (var (cat, leaf) in categoryTokens)
        {
            string token = "$category:" + cat;
            Add(new AttainableIngredient
            {
                QualifiedItemId = token,
                DisplayName = ModEntry.I18n.Get("quest.cooking.weeklySpecial.common.category." + leaf),
                CategoryId = cat,
                MatcherToken = token
            });
        }

        return result;
    }

    /// Ingredient count rules. Off: 1..4. On + Cooking Skill mod: 2..max(2, cookingLevel/2).
    /// On + no Cooking Skill: 2..5.
    private static int ResolveWeeklySpecialCommonIngredientCount(QuestContext ctx)
    {
        if (!ctx.Config.DifficultyScaling)
            return Game1.random.Next(1, 5);
        if (ModCompat.HasCookingSkill(ctx.Helper.ModRegistry))
        {
            int level = SpaceCoreSkills.GetLevel(Game1.player, "spacechase0.Cooking");
            int upper = Math.Max(2, level / 2);
            return Game1.random.Next(2, upper + 1);
        }
        return Game1.random.Next(2, 6);
    }

    /// Scores recipes by ingredient overlap with the picks. Highest score wins, random tiebreak.
    /// Returns null when no recipe scores 1+, so the caller can drop the dish flavour text.
    private static ResolvedItem? PickOverlapDish(QuestContext ctx, List<AttainableIngredient> picks)
    {
        var pickedCats = new HashSet<int>();
        foreach (var p in picks)
        {
            if (p.IsCategoryToken)
                pickedCats.Add(p.CategoryId);
            else if (p.Category != 0)
                pickedCats.Add(p.Category);
        }
        var pickedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in picks)
        {
            if (!p.IsCategoryToken && !string.IsNullOrEmpty(p.QualifiedItemId))
                pickedIds.Add(p.QualifiedItemId);
        }

        int bestScore = 0;
        var bestRecipes = new List<CookingRecipeInfo>();
        foreach (var recipe in ctx.Items.GetAllCookingRecipes())
        {
            int score = 0;
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.IsCategoryToken)
                {
                    if (pickedCats.Contains(ing.CategoryId))
                        score++;
                }
                else
                {
                    if (pickedIds.Contains(ing.Item.QualifiedItemId))
                        score++;
                }
            }
            if (score <= 0)
                continue;
            if (score > bestScore)
            {
                bestScore = score;
                bestRecipes.Clear();
                bestRecipes.Add(recipe);
            }
            else if (score == bestScore)
            {
                bestRecipes.Add(recipe);
            }
        }

        if (bestRecipes.Count == 0)
            return null;
        return bestRecipes[Game1.random.Next(bestRecipes.Count)].OutputItem;
    }

    /// Adds a per-NPC friendship bump for every met villager whose loved/liked list
    /// contains the dish. The saloon regulars who'd enjoy the week's dish appreciate the
    /// help. The consequence layer handles loved/hated reactions separately.
    private static void AddSaloonCrowdFriendship(QuestContext ctx, string dishQualifiedId, List<RewardSpec> rewards, int? perNpcMagnitude = null)
    {
        int magnitude = perNpcMagnitude ?? ctx.Config.FriendshipMultiSmall;
        string bareDish = dishQualifiedId.StartsWith("(O)") ? dishQualifiedId[3..] : dishQualifiedId;
        var tastes = ctx.Data.GiftTastes;
        foreach (var (npcName, _) in Game1.player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(npcName);
            if (npc == null || npc.IsMonster || !npc.IsVillager)
                continue;
            if (!tastes.TryGetValue(npcName, out var tasteData))
                continue;
            var fields = tasteData.Split('/');
            if (fields.Length < 4)
                continue;
            var loved = fields[1].Split(' ');
            var liked = fields[3].Split(' ');
            bool likes = loved.Contains(bareDish, StringComparer.Ordinal)
                        || liked.Contains(bareDish, StringComparer.Ordinal);
            if (!likes)
                continue;
            rewards.Add(new FriendshipReward(npcName, magnitude));
        }
    }

    /// SpecialOrder source. Picks N complex-tier recipes (including ones with category-token
    /// ingredients, since the Ship objective emits `category_&lt;N&gt;` context tags). Aggregates
    /// ingredients across dishes into one Ship objective per unique entry. One Tier 2
    /// consequence per dish (FriendshipIntermediate magnitude on the hated side). Reward:
    /// GoldExpertBase (vanilla path for the standard reward-box UX) + FriendshipMultiSmall
    /// to every villager who loves/likes any chosen dish (framework path, bypasses
    /// third-party SpecialOrder reward overrides).
    private static QuestPosting? GrandFeast(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        int wanted = ResolveGrandFeastRecipeCount(ctx);
        // Category-token recipes stay in the pool. The Ship objective emits `category_&lt;N&gt;`
        // tags so the bin can match modded items that carry the right category.
        var pool = ctx.Items.GetAllCookingRecipes()
            .Where(r => r.Ingredients.Count >= ModEntry.Config.WeeklySpecialComplexMinIngredients)
            .ToList();
        if (pool.Count < wanted)
            return null;

        var picked = new List<CookingRecipeInfo>(wanted);
        var available = new List<CookingRecipeInfo>(pool);
        for (int i = 0; i < wanted && available.Count > 0; i++)
        {
            int idx = Game1.random.Next(available.Count);
            picked.Add(available[idx]);
            available.RemoveAt(idx);
        }

        // Aggregate ingredients across recipes. Duplicates merge into one objective with
        // summed counts (one bigger pile of cheese instead of three smaller piles).
        var aggregate = new Dictionary<string, (string DisplayName, int Count, bool IsCategory, int CategoryId)>(StringComparer.Ordinal);
        foreach (var recipe in picked)
        {
            foreach (var ing in recipe.Ingredients)
            {
                string key = ing.Item.QualifiedItemId;
                if (aggregate.TryGetValue(key, out var existing))
                    aggregate[key] = (existing.DisplayName, existing.Count + ing.Count, existing.IsCategory, existing.CategoryId);
                else
                    aggregate[key] = (ing.Item.DisplayName, ing.Count, ing.IsCategoryToken, ing.CategoryId);
            }
        }

        var objectives = new List<SpecialOrderObjectiveSpec>(aggregate.Count);
        foreach (var (key, val) in aggregate)
        {
            string contextTag = val.IsCategory
                ? "category_" + Math.Abs(val.CategoryId)
                : "id_" + key.ToLowerInvariant().Replace("(", string.Empty).Replace(")", "_");
            objectives.Add(new SpecialOrderObjectiveSpec
            {
                Type = "Ship",
                Text = ModEntry.I18n.Get("quest.cooking.grandFeast.objective", new { count = val.Count, item = val.DisplayName }),
                RequiredCount = val.Count,
                Data = { ["AcceptedContextTags"] = contextTag }
            });
        }

        var dishNames = picked.Select(r => r.OutputItem.DisplayName).ToList();
        string dishesList = string.Join(", ", dishNames);

        // Money stays on vanilla's path for the standard reward-box UX. Friendship moves
        // to FrameworkRewards because friendship-tuning content packs (e.g. Special Order
        // Adjustments) overwrite vanilla `Friendship` entries on Data/SpecialOrders globally.
        // Routing through the framework path bypasses that interception.
        var vanillaRewards = new List<SpecialOrderRewardSpec>
        {
            new()
            {
                Type = "Money",
                Data = { ["Amount"] = ctx.Config.GoldExpertBase.ToString() }
            }
        };

        var frameworkRewards = new List<RewardSpec>();
        foreach (var dish in picked)
            AddSaloonCrowdFriendship(ctx, dish.OutputItem.QualifiedItemId, frameworkRewards);
        // Multiple dishes can share the same liked-by NPC. Collapse duplicates so the same
        // NPC isn't paid twice for the same completion.
        frameworkRewards = DedupeFriendshipRewards(frameworkRewards);

        var consequences = new List<ConsequenceSpec>();
        if (ModEntry.Config.ConsequencesEnabled)
        {
            foreach (var dish in picked)
            {
                consequences.Add(new ConsequenceSpec
                {
                    Tier = ConsequenceTier.Tier2,
                    Source = ConsequenceSource.GiftTastes,
                    Subject = dish.OutputItem.QualifiedItemId,
                    FriendshipOverride = -ctx.Config.FriendshipIntermediate,
                    LovedLine = ModEntry.I18n.Get(
                        "quest.cooking.grandFeast.consequence.loved",
                        new { dish = dish.OutputItem.DisplayName, npc = giver }),
                    HatedLine = ModEntry.I18n.Get(
                        "quest.cooking.grandFeast.consequence.hated",
                        new { dish = dish.OutputItem.DisplayName, npc = giver })
                });
            }
        }

        return new QuestPosting
        {
            Category = QuestCategory.Cooking,
            Tier = DifficultyTier.Expert,
            QuestType = BoardQuestType.Custom,
            Kind = PostingKind.SpecialOrder,
            QuestGiver = giver,
            SpecialOrder = new SpecialOrderSpec
            {
                Name = ModEntry.I18n.Get("quest.cooking.grandFeast.title", new { npc = giver }),
                Text = ModEntry.I18n.Get(
                    "quest.cooking.grandFeast.text",
                    new { npc = giver, dishes = dishesList }),
                Requester = giver,
                Duration = "Week",
                Objectives = objectives,
                Rewards = vanillaRewards,
                FrameworkRewards = frameworkRewards,
                Consequences = consequences
            }
        };
    }

    /// Recipe count. Scaling on + Cooking Skill: cookingLevel*3/2. Scaling on without
    /// Cooking Skill: 3..14. Scaling off: 2..6. Floored to 1 for brand-new saves.
    private static int ResolveGrandFeastRecipeCount(QuestContext ctx)
    {
        if (!ctx.Config.DifficultyScaling)
            return Game1.random.Next(2, 7);
        if (ModCompat.HasCookingSkill(ctx.Helper.ModRegistry))
        {
            int level = SpaceCoreSkills.GetLevel(Game1.player, "spacechase0.Cooking");
            return Math.Max(1, level * 3 / 2);
        }
        return Game1.random.Next(3, 15);
    }

    private static List<RewardSpec> DedupeFriendshipRewards(List<RewardSpec> rewards)
    {
        var byNpc = new Dictionary<string, int>(StringComparer.Ordinal);
        var others = new List<RewardSpec>(rewards.Count);
        foreach (var r in rewards)
        {
            if (r is FriendshipReward f)
            {
                byNpc.TryGetValue(f.Npc, out int total);
                byNpc[f.Npc] = total == 0 ? f.Points : Math.Max(total, f.Points);
            }
            else
            {
                others.Add(r);
            }
        }
        foreach (var (npc, amount) in byNpc)
            others.Add(new FriendshipReward(npc, amount));
        return others;
    }

    private static string Sanitise(string s) =>
        new string((s ?? string.Empty).Where(c => char.IsLetterOrDigit(c)).ToArray());

    /// AdventureQuest with one Deliver step per requested dish, gated on the giver's name so
    /// dishes land with one host. Dish count + per-dish quantity scale off Cooking Skill when
    /// installed, off Farming when not, and roll a small random when scaling is off. Dish pool
    /// widens to loved/liked/neutral so even prickly hosts have a workable menu. Reward:
    /// money proportional to dish sell prices + FriendshipMid to the giver.
    private static QuestPosting? DinnerParty(QuestContext ctx)
    {
        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;

        var tastes = ctx.Data.GiftTastes;
        var allRecipes = ctx.Items.GetAllCookingRecipes();
        if (allRecipes.Count == 0)
            return null;

        bool cookingSkillLoaded = ModCompat.HasCookingSkill(ctx.Helper.ModRegistry);
        int cookingLevel = cookingSkillLoaded
            ? SpaceCoreSkills.GetLevel(Game1.player, "spacechase0.Cooking")
            : 0;

        int wanted = ResolveDinnerPartyDishCount(ctx, cookingSkillLoaded, cookingLevel);
        int perCount = ResolveDinnerPartyPerDishCount(cookingSkillLoaded, cookingLevel);

        var shuffled = npcs.OrderBy(_ => Game1.random.Next()).ToList();
        foreach (var giver in shuffled)
        {
            if (!tastes.TryGetValue(giver, out var taste))
                continue;
            var fields = taste.Split('/');
            if (fields.Length < 10)
                continue;
            var loved = fields[1].Split(' ').ToHashSet(StringComparer.Ordinal);
            var liked = fields[3].Split(' ').ToHashSet(StringComparer.Ordinal);
            var neutral = fields[9].Split(' ').ToHashSet(StringComparer.Ordinal);

            var pool = new List<CookingRecipeInfo>();
            foreach (var r in allRecipes)
            {
                string bare = StripPrefix(r.OutputItem.QualifiedItemId);
                if (loved.Contains(bare) || liked.Contains(bare) || neutral.Contains(bare))
                    pool.Add(r);
            }
            if (pool.Count < wanted)
                continue;

            var picked = new List<CookingRecipeInfo>(wanted);
            var available = new List<CookingRecipeInfo>(pool);
            for (int i = 0; i < wanted && available.Count > 0; i++)
            {
                int idx = Game1.random.Next(available.Count);
                picked.Add(available[idx]);
                available.RemoveAt(idx);
            }

            var steps = new List<AdventureStepState>(picked.Count);
            var displayNames = new List<string>(picked.Count);
            int totalSell = 0;
            foreach (var dish in picked)
            {
                displayNames.Add(dish.OutputItem.DisplayName);
                totalSell += Math.Max(0, dish.OutputItem.SellPrice);
                steps.Add(new AdventureStepState
                {
                    Name = "Deliver" + StripPrefix(dish.OutputItem.QualifiedItemId),
                    Kind = AdventureStepKind.Deliver,
                    Targets = new List<string> { giver },
                    Items = new List<string> { dish.OutputItem.QualifiedItemId },
                    Count = perCount,
                    Description = ModEntry.I18n.Get(
                        "quest.cooking.dinnerParty.objective",
                        new { count = perCount, item = dish.OutputItem.DisplayName, npc = giver })
                });
            }

            int gold = (int)(totalSell * perCount * ctx.Config.RewardMultiplierAboveSell);
            if (gold < 100)
                gold = 100;

            string namesList = string.Join(", ", displayNames);
            string thanksDialogue = ModEntry.I18n.Get(
                "quest.cooking.dinnerParty.targetMessage",
                new { npc = giver }).ToString();

            var quest = new AdventureQuest();
            quest.Initialize(steps, giver: giver, completionDialogue: thanksDialogue);

            return new QuestPosting
            {
                Category = QuestCategory.Cooking,
                Tier = DifficultyTier.Advanced,
                QuestType = BoardQuestType.Adventure,
                QuestGiver = giver,
                ObjectiveQuantity = 1,
                DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
                Rewards =
                {
                    new MoneyReward(gold),
                    new FriendshipReward(giver, ctx.Config.FriendshipMid)
                },
                Title = ModEntry.I18n.Get("quest.cooking.dinnerParty.title", new { npc = giver }),
                Description = ModEntry.I18n.Get(
                    "quest.cooking.dinnerParty.description",
                    new { npc = giver, dishes = namesList, count = perCount }),
                TargetMessage = thanksDialogue,
                PreBuiltQuest = quest
            };
        }

        return null;
    }

    /// Dish count. Scaling on: 2 + cookingLevel/2 (Cooking Skill) or 2 + farmingLevel/2.
    /// Scaling off: 1..3.
    private static int ResolveDinnerPartyDishCount(QuestContext ctx, bool cookingSkillLoaded, int cookingLevel)
    {
        if (!ctx.Config.DifficultyScaling)
            return Game1.random.Next(1, 4);
        int level = cookingSkillLoaded ? cookingLevel : Game1.player.FarmingLevel;
        return Math.Max(1, 2 + level / 2);
    }

    /// Per-dish count. With Cooking Skill: cookingLevel/2 (min 1). Otherwise random 1..3.
    private static int ResolveDinnerPartyPerDishCount(bool cookingSkillLoaded, int cookingLevel)
    {
        if (!cookingSkillLoaded)
            return Game1.random.Next(1, 4);
        return Math.Max(1, cookingLevel / 2);
    }

}
