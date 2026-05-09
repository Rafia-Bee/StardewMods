using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
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
        if (!ConditionEvaluator.KnowsAnyCookingRecipe())
            return null;

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

    /// CSV row 35. Daily-board AdventureQuest. The picked saloon NPC asks the player to
    /// bring the ingredients for one randomly-chosen common-tier recipe (≤
    /// `WeeklySpecialCommonMaxIngredients` distinct ingredients). One Deliver step per
    /// ingredient. Reward = `GoldBeginnerBase` + `FriendshipMultiSmall` to every met
    /// villager who loves or likes the cooked dish (the saloon-going crowd who'd actually
    /// eat the special). Tier 1 consequence keys to the dish output id — the Sample-One
    /// rule in `ConsequenceEngine` keeps the fanfare to one randomly-picked NPC across the
    /// loved + hated union.
    private static QuestPosting? WeeklySpecialCommon(QuestContext ctx)
    {
        return BuildWeeklySpecial(
            ctx,
            tier: DifficultyTier.Beginner,
            consequenceTier: ConsequenceTier.Tier1,
            goldBase: ctx.Config.GoldBeginnerBase,
            deadlineKind: DeadlineKind.Short,
            minIngredients: 1,
            maxIngredients: ModEntry.Config.WeeklySpecialCommonMaxIngredients,
            titleKey: "quest.cooking.weeklySpecial.common.title",
            descriptionKey: "quest.cooking.weeklySpecial.common.description",
            consequenceLovedKey: "quest.cooking.weeklySpecial.common.consequence.loved",
            consequenceHatedKey: "quest.cooking.weeklySpecial.common.consequence.hated");
    }

    /// CSV row 36. Same shape as the Common variant but the recipe pool is filtered to
    /// `WeeklySpecialComplexMinIngredients`+ distinct ingredients, the gold base steps up
    /// to `GoldIntermediateBase`, the deadline grows to `Medium`, and the consequence
    /// jumps to Tier 2 — loved NPCs get `+FriendshipBasic`, hated NPCs get the Tier 2
    /// default negative delta (mid between `FriendshipBasic` and `FriendshipMid`).

    /// CSV row 36. Same shape as the Common variant but the recipe pool is filtered to
    /// `WeeklySpecialComplexMinIngredients`+ distinct ingredients, the gold base steps up
    /// to `GoldIntermediateBase`, the deadline grows to `Medium`, and the consequence
    /// jumps to Tier 2 — loved NPCs get `+FriendshipBasic`, hated NPCs get the Tier 2
    /// default negative delta (mid between `FriendshipBasic` and `FriendshipMid`).
    private static QuestPosting? WeeklySpecialComplex(QuestContext ctx)
    {
        return BuildWeeklySpecial(
            ctx,
            tier: DifficultyTier.Advanced,
            consequenceTier: ConsequenceTier.Tier2,
            goldBase: ctx.Config.GoldIntermediateBase,
            deadlineKind: DeadlineKind.Medium,
            minIngredients: ModEntry.Config.WeeklySpecialComplexMinIngredients,
            maxIngredients: int.MaxValue,
            titleKey: "quest.cooking.weeklySpecial.complex.title",
            descriptionKey: "quest.cooking.weeklySpecial.complex.description",
            consequenceLovedKey: "quest.cooking.weeklySpecial.complex.consequence.loved",
            consequenceHatedKey: "quest.cooking.weeklySpecial.complex.consequence.hated");
    }

    private static QuestPosting? BuildWeeklySpecial(
        QuestContext ctx,
        DifficultyTier tier,
        ConsequenceTier consequenceTier,
        int goldBase,
        DeadlineKind deadlineKind,
        int minIngredients,
        int maxIngredients,
        string titleKey,
        string descriptionKey,
        string consequenceLovedKey,
        string consequenceHatedKey)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        var pool = ctx.Items.GetAllCookingRecipes()
            .Where(r => r.Ingredients.Count >= minIngredients && r.Ingredients.Count <= maxIngredients)
            .ToList();
        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];

        var steps = new List<AdventureStepState>(pick.Ingredients.Count);
        foreach (var ing in pick.Ingredients)
        {
            string token = ing.IsCategoryToken ? ing.Item.QualifiedItemId : ing.Item.QualifiedItemId;
            steps.Add(new AdventureStepState
            {
                Name = "Deliver_" + Sanitise(ing.Item.DisplayName),
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { giver },
                Items = new List<string> { token },
                Count = ing.Count,
                Description = ModEntry.I18n.Get(
                    "quest.cooking.weeklySpecial.step.deliver",
                    new { count = ing.Count, item = ing.Item.DisplayName, npc = giver })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(
            steps,
            giver: giver,
            completionDialogue: ModEntry.I18n.Get(
                "quest.cooking.weeklySpecial.targetMessage",
                new { dish = pick.OutputItem.DisplayName }));

        var rewards = new List<RewardSpec> { new MoneyReward(goldBase) };
        AddSaloonCrowdFriendship(ctx, pick.OutputItem.QualifiedItemId, rewards);

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            consequence = new ConsequenceSpec
            {
                Tier = consequenceTier,
                Source = ConsequenceSource.GiftTastes,
                Subject = pick.OutputItem.QualifiedItemId,
                LovedLine = ModEntry.I18n.Get(
                    consequenceLovedKey,
                    new { dish = pick.OutputItem.DisplayName, npc = giver }),
                HatedLine = ModEntry.I18n.Get(
                    consequenceHatedKey,
                    new { dish = pick.OutputItem.DisplayName, npc = giver })
            };
        }

        string ingredientsList = string.Join(", ", pick.Ingredients
            .Select(i => i.Count + " " + i.Item.DisplayName));

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
            Description = ModEntry.I18n.Get(
                descriptionKey,
                new { npc = giver, dish = pick.OutputItem.DisplayName, ingredients = ingredientsList }),
            TargetMessage = ModEntry.I18n.Get(
                "quest.cooking.weeklySpecial.targetMessage",
                new { dish = pick.OutputItem.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Adds a per-NPC `FriendshipMultiSmall` reward for every met human villager whose
    /// `Data/NPCGiftTastes` entry has the dish in its loved or liked list. Models the
    /// CSV's "FriendshipMultiSmall with multiple NPCs" reward column — the saloon
    /// regulars who'd enjoy that week's dish appreciate the help. NPCs who hate the
    /// dish or are indifferent are skipped on the reward side; the consequence layer
    /// handles the loved-or-hated reactions separately.

    /// Adds a per-NPC `FriendshipMultiSmall` reward for every met human villager whose
    /// `Data/NPCGiftTastes` entry has the dish in its loved or liked list. Models the
    /// CSV's "FriendshipMultiSmall with multiple NPCs" reward column — the saloon
    /// regulars who'd enjoy that week's dish appreciate the help. NPCs who hate the
    /// dish or are indifferent are skipped on the reward side; the consequence layer
    /// handles the loved-or-hated reactions separately.
    private static void AddSaloonCrowdFriendship(QuestContext ctx, string dishQualifiedId, List<RewardSpec> rewards)
    {
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
            rewards.Add(new FriendshipReward(npcName, ctx.Config.FriendshipMultiSmall));
        }
    }

    /// CSV row 34. SpecialOrder source. Picks `GrandFeastRecipeCount` distinct complex-
    /// tier recipes (same pool as the Complex Weekly Special); aggregates their
    /// ingredients into one Ship objective per unique ingredient (using vanilla
    /// `id_o_<id>` context tags so the shipping bin counts modded items too); seeds one
    /// Tier 2 consequence per dish so each sampled NPC reacts to a different dish across
    /// the post-completion week. Reward = `GoldExpertBase` (vanilla path so the player
    /// gets the standard reward-box UX) + `FriendshipMultiSmall` to every met villager
    /// who loves or likes any of the chosen dishes (framework path, bypasses third-party
    /// SpecialOrder reward overrides).

    /// CSV row 34. SpecialOrder source. Picks `GrandFeastRecipeCount` distinct complex-
    /// tier recipes (same pool as the Complex Weekly Special); aggregates their
    /// ingredients into one Ship objective per unique ingredient (using vanilla
    /// `id_o_<id>` context tags so the shipping bin counts modded items too); seeds one
    /// Tier 2 consequence per dish so each sampled NPC reacts to a different dish across
    /// the post-completion week. Reward = `GoldExpertBase` (vanilla path so the player
    /// gets the standard reward-box UX) + `FriendshipMultiSmall` to every met villager
    /// who loves or likes any of the chosen dishes (framework path, bypasses third-party
    /// SpecialOrder reward overrides).
    private static QuestPosting? GrandFeast(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        int wanted = Math.Max(1, ModEntry.Config.GrandFeastRecipeCount);
        // Grand Feast translates each ingredient into a vanilla SpecialOrder Ship objective
        // keyed off the auto-generated `id_o_<id>` context tag. Category-token ingredients
        // (negative ids like -5 = egg, -6 = milk) don't have a single literal context tag
        // we can ship-match against, so recipes containing one are dropped from this pool —
        // they remain eligible for the AdventureQuest-based Weekly Special variants where
        // the `$category:N` token resolves at gift/deliver time.
        var pool = ctx.Items.GetAllCookingRecipes()
            .Where(r => r.Ingredients.Count >= ModEntry.Config.WeeklySpecialComplexMinIngredients)
            .Where(r => r.Ingredients.All(i => !i.IsCategoryToken))
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

        // Aggregate ingredients across the picked recipes — duplicates merge into one
        // objective with summed counts so the player ships one bigger pile of cheese
        // instead of three smaller piles.
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

        // Money stays in vanilla's path so the reward-box UX is the standard one. The
        // friendship payout moves to FrameworkRewards because friendship-tuning content
        // packs (e.g. Special Order Adjustments) overwrite vanilla `Friendship` reward
        // entries on `Data/SpecialOrders` globally — routing through the framework path
        // bypasses that interception entirely.
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
        // Multiple dishes can share the same liked-by NPC — collapse duplicate
        // FriendshipReward entries so the same NPC isn't paid twice for the same
        // SpecialOrder completion.
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

    // -------------------- Phase 9c: Fishing ecology + Monster Parts --------------------

    /// CSV row 49. Daily-board fishing quest. Pierre or Joja (Morris/MorrisTod) ask the
    /// player for a bulk haul of one specific seasonal fish; reward is sell-price scaled
    /// below market (`RewardMultiplierBelowSell`). Tier 2 ecology consequence: every
    /// member of the `EcologyMinded` pool present on the save (Demetrius + RSV's Maddie /
    /// Mr. Aguar + East Scarp's Dylan) gets a single negative line + the Tier 2 default
    /// negative friendship delta on the next chat. Linus is intentionally excluded — the
    /// plan reserves him for Tier 3 (Seafood Night).

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

    // -------------------- Phase 9.5a: Declarative quick-wins --------------------

    /// CSV row 28. Daily-board ItemDelivery. Anonymous board posting from one met NPC
    /// asking the player to deliver a small gift to a different met NPC. Item is picked
    /// from the recipient's loved or liked list so the gift always lands well. Reward =
    /// `RewardMultiplierBelowSell` × the item's sell price (the giver "covers the cost
    /// minus a finder's fee") plus `FriendshipBasic` with the recipient.

    /// CSV row 18. SpecialOrder source. Smaller cousin of the Grand Feast: picks
    /// `DinnerPartyDishCount` (default 3) distinct dishes the giver Loves or Likes per
    /// `Data/NPCGiftTastes` and emits one vanilla `Deliver` objective per dish targeted at
    /// the giver. Reward = sum(dish sell price) * `RewardMultiplierAboveSell` (vanilla
    /// path so the player gets the standard reward-box UX) + `FriendshipBasic` to the
    /// giver (framework path, bypasses third-party SpecialOrder reward overrides).
    ///
    /// Mirrors `Cooking.GrandFeast`'s JSON shape (no `StartDate`); the trigger evaluator's
    /// `SpecialOrderReady` requires a `StartDate` for auto-fire, so the order is currently
    /// reachable through the framework's `mq_reemit_specialorders` debug command. A
    /// follow-up in §13 should add a cooldown-only SpecialOrder mode so daily-cadence
    /// special orders fire automatically; that's a framework change tracked separately.
    private static QuestPosting? DinnerParty(QuestContext ctx)
    {
        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;

        var tastes = ctx.Data.GiftTastes;
        var allRecipes = ctx.Items.GetAllCookingRecipes();
        if (allRecipes.Count == 0)
            return null;

        int wanted = Math.Max(1, ModEntry.Config.DinnerPartyDishCount);
        int perCount = Math.Max(1, ModEntry.Config.DinnerPartyPerDishCount);

        var shuffled = npcs.OrderBy(_ => Game1.random.Next()).ToList();
        foreach (var giver in shuffled)
        {
            if (!tastes.TryGetValue(giver, out var taste))
                continue;
            var fields = taste.Split('/');
            if (fields.Length < 4)
                continue;
            var loved = fields[1].Split(' ').ToHashSet(StringComparer.Ordinal);
            var liked = fields[3].Split(' ').ToHashSet(StringComparer.Ordinal);

            var pool = new List<CookingRecipeInfo>();
            foreach (var r in allRecipes)
            {
                string bare = StripPrefix(r.OutputItem.QualifiedItemId);
                if (loved.Contains(bare) || liked.Contains(bare))
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

            var objectives = new List<SpecialOrderObjectiveSpec>(picked.Count);
            var displayNames = new List<string>(picked.Count);
            int totalSell = 0;
            foreach (var dish in picked)
            {
                string bare = StripPrefix(dish.OutputItem.QualifiedItemId);
                string contextTag = "id_o_" + bare.ToLowerInvariant();
                displayNames.Add(dish.OutputItem.DisplayName);
                totalSell += Math.Max(0, dish.OutputItem.SellPrice);
                objectives.Add(new SpecialOrderObjectiveSpec
                {
                    Type = "Deliver",
                    Text = ModEntry.I18n.Get(
                        "quest.cooking.dinnerParty.objective",
                        new { count = perCount, item = dish.OutputItem.DisplayName, npc = giver }),
                    RequiredCount = perCount,
                    Data =
                    {
                        ["AcceptedContextTags"] = contextTag,
                        ["TargetName"] = giver
                    }
                });
            }

            int gold = (int)(totalSell * perCount * ctx.Config.RewardMultiplierAboveSell);
            if (gold < 100)
                gold = 100;

            var vanillaRewards = new List<SpecialOrderRewardSpec>
            {
                new()
                {
                    Type = "Money",
                    Data = { ["Amount"] = gold.ToString() }
                }
            };
            var frameworkRewards = new List<RewardSpec>
            {
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            };

            string namesList = string.Join(", ", displayNames);

            return new QuestPosting
            {
                Category = QuestCategory.Cooking,
                Tier = DifficultyTier.Advanced,
                QuestType = BoardQuestType.Custom,
                Kind = PostingKind.SpecialOrder,
                QuestGiver = giver,
                SpecialOrder = new SpecialOrderSpec
                {
                    Name = ModEntry.I18n.Get("quest.cooking.dinnerParty.title", new { npc = giver }),
                    Text = ModEntry.I18n.Get(
                        "quest.cooking.dinnerParty.text",
                        new { npc = giver, dishes = namesList }),
                    Requester = giver,
                    Duration = "Week",
                    Objectives = objectives,
                    Rewards = vanillaRewards,
                    FrameworkRewards = frameworkRewards
                }
            };
        }

        return null;
    }

    /// CSV row 55. Daily-board single-step `Plant` AdventureQuest. Quest giver picked
    /// from the `ConservationGuide` dispatch role (Linus / Demetrius / Kimpoi RSV /
    /// Dylan ESV / Aster VMV) — exactly the CSV's listed givers. Player must plant
    /// `PlantTreesCount` trees at `PlantTreesLocation` (default Cindersap Forest).
    /// Reward = `FriendshipIntermediate` to the giver — pure friendship, no gold per
    /// the CSV. The `Plant` step rides `World.TerrainFeatureListChanged` filter Tree.
}
