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

/// Central registration point for all C# quest generators referenced by `assets/quests.json`.
/// Each method below is the `Build()` body that previously lived in its own
/// `IQuestDefinition` class. Phase 4 migration: metadata (Id/Category/Kind/Weight/MaxPerDay/
/// CooldownDays/Available) now lives in JSON; the C# side owns only runtime randomization.
internal static class Generators
{
    public static void RegisterAll(IMoreQuestsModApi fw)
    {
        fw.RegisterGenerator("BasicCropDelivery", BasicCropDelivery);
        fw.RegisterGenerator("SimpleFishingRequest", SimpleFishingRequest);
        fw.RegisterGenerator("BasicSlimeClearing", BasicSlimeClearing);
        fw.RegisterGenerator("BarDelivery", BarDelivery);
        fw.RegisterGenerator("SeasonalForaging", SeasonalForaging);
        fw.RegisterGenerator("ElliottPoemInspiration", ElliottPoemInspiration);
        fw.RegisterGenerator("CheckOnGeorge", CheckOnGeorge);
        fw.RegisterGenerator("HaySupplyRun", HaySupplyRun);
        fw.RegisterGenerator("BeachCleanup", BeachCleanup);
        fw.RegisterGenerator("SpringTea", SpringTea);
        fw.RegisterGenerator("CravingDish", CravingDishGenerator);
        fw.RegisterGenerator("SubmarineFuel", SubmarineFuel);
        fw.RegisterGenerator("WizardsRitualMaterials", WizardsRitualMaterials);
        fw.RegisterGenerator("HolidayCookies", HolidayCookies);
        fw.RegisterGenerator("CheckOnFriends", CheckOnFriends);
        fw.RegisterGenerator("GusFestivalFeastSpring", GusFestivalFeastSpring);
        fw.RegisterGenerator("GusFestivalFeastWinter", GusFestivalFeastWinter);
        fw.RegisterGenerator("PreservesSeason", PreservesSeason);
        fw.RegisterGenerator("SkullCavernDeepDive", SkullCavernDeepDive);
        fw.RegisterGenerator("MinesDeepDive", MinesDeepDive);
        fw.RegisterGenerator("PierresStockUp", PierresStockUp);
        fw.RegisterGenerator("MassiveHarvestRequest", MassiveHarvestRequest);
        fw.RegisterGenerator("WeeklySpecialCommon", WeeklySpecialCommon);
        fw.RegisterGenerator("WeeklySpecialComplex", WeeklySpecialComplex);
        fw.RegisterGenerator("GrandFeast", GrandFeast);
        fw.RegisterGenerator("MediumFishingHaul", MediumFishingHaul);
        fw.RegisterGenerator("SeafoodNight", SeafoodNight);
        fw.RegisterGenerator("MonsterParts", MonsterParts);
        fw.RegisterGenerator("ForageWithLinus", ForageWithLinus);
        fw.RegisterGenerator("GusFestivalFeastFall", GusFestivalFeastFall);
        fw.RegisterGenerator("GusFestivalFeastSummer", GusFestivalFeastSummer);
        fw.RegisterGenerator("GiftDelivery", GiftDelivery);
        fw.RegisterGenerator("HeatWaveRelief", HeatWaveRelief);
        fw.RegisterGenerator("JellyfishWatchPrep", JellyfishWatchPrep);
        fw.RegisterGenerator("MerchantUnpacking", MerchantUnpacking);
        fw.RegisterGenerator("MonsterHunt", MonsterHunt);
        fw.RegisterGenerator("RareForageHunt", RareForageHunt);
        fw.RegisterGenerator("RareMaterialRequest", RareMaterialRequest);
        fw.RegisterGenerator("SecretGiftHint", SecretGiftHint);
        fw.RegisterGenerator("WrappingPaper", WrappingPaper);
        fw.RegisterGenerator("PremiumCropOrder", PremiumCropOrder);
        fw.RegisterGenerator("QualityCropDelivery", QualityCropDelivery);
        fw.RegisterGenerator("QualityFishDelivery", QualityFishDelivery);
        fw.RegisterGenerator("MoonlightJelliesFestivalDecor", MoonlightJelliesFestivalDecor);
        fw.RegisterGenerator("EggFestivalDecor", EggFestivalDecor);
        fw.RegisterGenerator("FairFestivalDecor", FairFestivalDecor);
        fw.RegisterGenerator("LuauFestivalDecor", LuauFestivalDecor);
        fw.RegisterGenerator("SpiritsEveDecor", SpiritsEveDecor);
        fw.RegisterGenerator("EastScarpSpiritsEveDecor", EastScarpSpiritsEveDecor);
        fw.RegisterGenerator("RidgesideGatheringDecor", RidgesideGatheringDecor);
        fw.RegisterGenerator("LocationFishOverpopulation", LocationFishOverpopulation);
        fw.RegisterGenerator("RainyDayCatch", RainyDayCatch);
        fw.RegisterGenerator("SizeFishOverpopulation", SizeFishOverpopulation);
        fw.RegisterGenerator("RainbowPlatter", RainbowPlatter);
        fw.RegisterGenerator("SquidFestShowcase", SquidFestShowcase);
    }

    // -------------------- Farming --------------------

    private static QuestPosting? BasicCropDelivery(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;

        var crop = crops[Game1.random.Next(crops.Count)];
        int skill = Difficulty.GetSkillLevel(QuestCategory.Farming);
        var tier = Difficulty.TierForSkill(Math.Min(skill, 3));

        int qty = tier switch
        {
            DifficultyTier.Beginner => Game1.random.Next(3, 7),
            DifficultyTier.Intermediate => Game1.random.Next(6, 10),
            _ => Game1.random.Next(8, 20)
        };

        int basePrice = Math.Max(crop.SellPrice, 30);
        int gold = (int)(basePrice * qty * ctx.Config.RewardMultiplierAboveSell);

        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = tier,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.farming.basic.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.farming.basic.description", new { npc = giver, qty, item = crop.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.farming.basic.objective", new { npc = giver, qty, item = crop.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.basic.targetMessage")
        };
    }

    // -------------------- Fishing --------------------

    private static QuestPosting? SimpleFishingRequest(QuestContext ctx)
    {
        var fish = ctx.Config.FishingIgnoresVisitedLocations
            ? ctx.Items.GetSeasonalFish(ctx.Season)
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season);
        if (fish.Count == 0)
            return null;

        fish.Sort((a, b) => a.Difficulty.CompareTo(b.Difficulty));
        var pool = fish.GetRange(0, Math.Min(fish.Count, Math.Max(3, fish.Count / 2)));
        var target = pool[Game1.random.Next(pool.Count)];

        int qty = Game1.random.Next(1, 4);
        int gold = (int)(target.SellPrice * qty * ctx.Config.RewardMultiplierAboveSell);

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = "Willy",
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.fishing.simple.title"),
            Description = ModEntry.I18n.Get("quest.fishing.simple.description", new { npc = "Willy", qty, item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.simple.objective", new { qty, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.simple.targetMessage")
        };
    }

    // -------------------- Mining --------------------

    private static QuestPosting? BasicSlimeClearing(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.CombatVendor);
        if (giver == null)
            return null;

        int qty = Game1.random.Next(8, 16);
        int gold = ctx.Config.GoldBeginnerBase + Game1.random.Next(0, 100);

        var quest = new AnySlimeQuest
        {
            target = { Value = giver },
            monsterName = { Value = "Green Slime" },
            numberToKill = { Value = qty },
            reward = { Value = gold },
            targetMessage = ModEntry.I18n.Get("quest.mining.slime.targetMessage", new { npc = giver })
        };

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = giver,
            ObjectiveItemId = "Green Slime",
            ObjectiveItemName = "Green Slime",
            ObjectiveQuantity = qty,
            TargetMonster = "Green Slime",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.mining.slime.title"),
            Description = ModEntry.I18n.Get("quest.mining.slime.description", new { qty, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.slime.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.slime.targetMessage", new { npc = giver }),
            PreBuiltQuest = quest
        };
    }

    private static readonly (string Id, string Name)[] BarPool =
    {
        ("(O)334", "Copper Bar"),
        ("(O)335", "Iron Bar"),
        ("(O)336", "Gold Bar"),
        ("(O)337", "Iridium Bar")
    };

    private static QuestPosting? BarDelivery(QuestContext ctx)
    {
        int level = Game1.player.MiningLevel;
        bool skullCavernUnlocked = Game1.player.deepestMineLevel > 120;

        int maxIdxExclusive = skullCavernUnlocked ? 4 : 3;
        int barIdx = level switch
        {
            >= 8 => Game1.random.Next(2, maxIdxExclusive),
            >= 4 => Game1.random.Next(1, Math.Min(3, maxIdxExclusive)),
            _ => 0
        };
        var bar = BarPool[barIdx];

        int qty = Game1.random.Next(2, 5);
        int gold = ctx.Config.GoldIntermediateBase;

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Clint",
            ObjectiveItemId = bar.Id,
            ObjectiveItemName = bar.Name,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            // TODO: reward should be gold + a geode or gem.
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.mining.bar.title"),
            Description = ModEntry.I18n.Get("quest.mining.bar.description", new { qty, item = bar.Name }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.bar.objective", new { qty, item = bar.Name }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.bar.targetMessage")
        };
    }

    // -------------------- Foraging --------------------

    private static QuestPosting? SeasonalForaging(QuestContext ctx)
    {
        var pool = ctx.Items.GetForageItems(ctx.Season);
        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];
        int qty = Game1.random.Next(3, 8);
        int gold = ctx.Config.GoldBeginnerBase;

        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        return new QuestPosting
        {
            Category = QuestCategory.Foraging,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.foraging.seasonal.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.foraging.seasonal.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.foraging.seasonal.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.foraging.seasonal.targetMessage")
        };
    }

    // -------------------- Social --------------------

    private static QuestPosting? ElliottPoemInspiration(QuestContext ctx)
    {
        var pool = new List<ResolvedItem>();
        pool.AddRange(ctx.Items.GetItemsByCategory(StardewValley.Object.flowersCategory));
        pool.AddRange(ctx.Items.GetItemsByCategory(StardewValley.Object.GemCategory));

        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Elliott",
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward("Elliott", ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.social.elliott.title"),
            Description = ModEntry.I18n.Get("quest.social.elliott.description", new { item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.social.elliott.objective", new { item = pick.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.social.elliott.targetMessage")
        };
    }

    private static QuestPosting? CheckOnGeorge(QuestContext ctx)
    {
        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "GiftGeorge",
                Kind = AdventureStepKind.Gift,
                Targets = new List<string> { "George" },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.social.george.step.gift")
            },
            new AdventureStepState
            {
                Name = "TalkGeorge",
                Kind = AdventureStepKind.Talk,
                Targets = new List<string> { "George" },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.social.george.step.talkGeorge")
            },
            new AdventureStepState
            {
                Name = "ReportEvelyn",
                Kind = AdventureStepKind.Talk,
                Targets = new List<string> { "Evelyn" },
                Requires = new List<string> { "GiftGeorge", "TalkGeorge" },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.social.george.step.reportEvelyn")
            }
        }, giver: "Evelyn", completionDialogue: ModEntry.I18n.Get("quest.social.george.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Evelyn",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Evelyn", ctx.Config.FriendshipMid),
                new FriendshipReward("George", ctx.Config.FriendshipMid)
            },
            Title = ModEntry.I18n.Get("quest.social.george.title"),
            Description = ModEntry.I18n.Get("quest.social.george.description"),
            CurrentObjective = ModEntry.I18n.Get("quest.social.george.step.gift"),
            TargetMessage = ModEntry.I18n.Get("quest.social.george.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Animal --------------------

    private static QuestPosting? HaySupplyRun(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        int animals = CountAnimals();
        if (animals < 4)
            return null;

        int qty = Math.Max(ModEntry.Config.HaySupplyBaseQty, animals * 3);
        int gold = (int)(qty * 50 * 0.8);

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)178",
            ObjectiveItemName = "Hay",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.animal.hay.title"),
            Description = ModEntry.I18n.Get("quest.animal.hay.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.hay.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.hay.targetMessage")
        };
    }

    private static int CountAnimals()
    {
        int total = 0;
        foreach (var location in Game1.locations)
        {
            total += location.animals.Count();
            foreach (var building in location.buildings)
            {
                var indoor = building.GetIndoors();
                if (indoor != null)
                    total += indoor.animals.Count();
            }
        }
        return total;
    }

    // -------------------- Seasonal --------------------

    private static readonly (string Id, string Name)[] BeachForage =
    {
        ("(O)393", "Coral"),
        ("(O)397", "Sea Urchin"),
        ("(O)392", "Nautilus Shell"),
        ("(O)394", "Rainbow Shell"),
        ("(O)372", "Clam"),
        ("(O)718", "Cockle"),
        ("(O)719", "Mussel"),
        ("(O)723", "Oyster")
    };

    private static QuestPosting? BeachCleanup(QuestContext ctx)
    {
        var pick = BeachForage[Game1.random.Next(BeachForage.Length)];
        int qty = Game1.random.Next(2, 6);

        string? giver = ctx.Dispatch.Pick(DispatchRoles.BeachCleanup);
        if (giver == null)
            return null;

        var quest = new CollectAndReportQuest
        {
            talkToNpc = { Value = giver },
            requiredCount = { Value = qty },
            reportMessage = { Value = ModEntry.I18n.Get("quest.seasonal.beach.targetMessage") }
        };
        quest.itemIds.Add(pick.Id);

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = giver,
            ObjectiveItemId = pick.Id,
            ObjectiveItemName = pick.Name,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.seasonal.beach.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.beach.description", new { npc = giver, qty, item = pick.Name }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.beach.objective", new { qty, item = pick.Name, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.beach.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    private static QuestPosting? SpringTea(QuestContext ctx)
    {
        var allFlowers = ctx.Items.GetItemsByCategory(StardewValley.Object.flowersCategory);
        var springFlowers = allFlowers
            .Where(f => f.ContextTags.Contains("season_spring"))
            .ToList();

        if (springFlowers.Count == 0)
            return null;

        var pick = springFlowers[Game1.random.Next(springFlowers.Count)];
        int qty = Game1.random.Next(3, 6);

        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.seasonal.springtea.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.springtea.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.springtea.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.springtea.targetMessage")
        };
    }

    // -------------------- Cooking --------------------

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

    private static string StripPrefix(string id) =>
        id.StartsWith("(O)") ? id[3..] : id;

    // -------------------- Festival quests (Phase 7b) --------------------

    /// Single-objective Ship quest. Player ships Battery Pack OR Coal into the farm
    /// shipping bin; the framework's DayEnding observer credits each match by weight,
    /// where one Battery Pack equals 15 Coal of "fuel". The reward Pearl arrives by mail
    /// the next morning. Mining-skill scaling: base = 15 fuel (= 1 battery / 15 coal),
    /// scales 1.5× per mining level when DifficultyScaling is on. With scaling off it's
    /// a fixed 30 fuel target (= 2 batteries / 30 coal).
    private static QuestPosting? SubmarineFuel(QuestContext ctx)
    {
        const int batteryWeight = 15;
        int totalFuel = ctx.Config.DifficultyScaling
            ? Math.Max(15, (int)Math.Floor(15 * 1.5 * Game1.player.MiningLevel))
            : 30;

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Ship,
            QuestGiver = "Captain",
            ObjectiveItemId = "(O)787",
            ObjectiveItemName = "Battery Pack",
            ObjectiveItemWeight = batteryWeight,
            AlternativeObjectiveItemIds = { "(O)382" },
            AlternativeObjectiveItemWeights = { 1 },
            ObjectiveQuantity = totalFuel,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new MailReward("RafiaBee.MoreQuests.SubmarineFuelReward", MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.festival.submarineFuel.title"),
            Description = ModEntry.I18n.Get("quest.festival.submarineFuel.description"),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.submarineFuel.objective", new { batteries = Math.Max(1, (int)Math.Ceiling(totalFuel / (double)batteryWeight)), coal = totalFuel }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.submarineFuel.targetMessage")
        };
    }

    /// Multi-step Ship Adventure: ship Void Essence + Bat Wings + Solar Essence to the
    /// shipping bin, in any order. Combat-skill scaling: count = max(1, 2 × CombatLevel)
    /// per item when DifficultyScaling is on; fixed 3 of each when off. Reward = Book of
    /// Mysteries via inventory add on completion.
    private static QuestPosting? WizardsRitualMaterials(QuestContext ctx)
    {
        int countPer = ctx.Config.DifficultyScaling
            ? Math.Max(1, 2 * Game1.player.CombatLevel)
            : 3;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipVoidEssence",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)769" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.step.voidEssence", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "ShipBatWing",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)767" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.step.batWing", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "ShipSolarEssence",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)768" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.step.solarEssence", new { count = countPer })
            }
        }, giver: "M. Rasmodius");

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "M. Rasmodius",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new MailReward("RafiaBee.MoreQuests.WizardsRitualReward", MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.festival.wizardsRitual.title"),
            Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.description", new { count = countPer }),
            PreBuiltQuest = quest
        };
    }

    /// Multi-step Deliver Adventure: hand Flour + Sugar + Egg to Evelyn in any order.
    /// Farming-skill scaling: count = max(3, 6 × FarmingLevel) per ingredient when
    /// DifficultyScaling is on; fixed 3 of each when off. Egg step accepts ANY edible egg
    /// (Object Category -5 with non-inedible Edibility) via the `$edible-egg` token, so
    /// modded eggs (Hootin' & Hollerin' Owl, SVE Goose, VMV Speckled Fowl, etc.) all
    /// count alongside the vanilla white/brown chicken / duck / ostrich / void eggs.
    /// Dinosaur Egg is excluded because vanilla marks it `Edibility = -300` (inedible).
    private static QuestPosting? HolidayCookies(QuestContext ctx)
    {
        int countPer = ctx.Config.DifficultyScaling
            ? Math.Max(3, 6 * Game1.player.FarmingLevel)
            : 3;

        var eggIds = new List<string> { "$edible-egg" };

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverFlour",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Evelyn" },
                Items = new List<string> { "(O)246" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.holidayCookies.step.flour", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "DeliverSugar",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Evelyn" },
                Items = new List<string> { "(O)245" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.holidayCookies.step.sugar", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "DeliverEggs",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Evelyn" },
                Items = eggIds,
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.holidayCookies.step.eggs", new { count = countPer })
            }
        }, giver: "Evelyn", completionDialogue: ModEntry.I18n.Get("quest.festival.holidayCookies.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Evelyn",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Evelyn", ctx.Config.FriendshipLarge),
                new ObjectReward("(O)223", 6)
            },
            Title = ModEntry.I18n.Get("quest.festival.holidayCookies.title"),
            Description = ModEntry.I18n.Get("quest.festival.holidayCookies.description", new { count = countPer }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.holidayCookies.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 7c: Check on Friends --------------------

    /// Two-step Adventure: pick N met villagers + a separate giver, talk to all N (in
    /// any order — the Talk step's CreditedKeys enforces uniqueness so the same NPC
    /// can't be counted twice), then report back to the giver. Requires at least
    /// CheckOnFriendsCount + 1 met villagers (otherwise there are fewer NPCs than the
    /// quest expects). Reward = FriendshipIntermediate to the giver only.
    private static QuestPosting? CheckOnFriends(QuestContext ctx)
    {
        const int n = 3;
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count < n + 1)
            return null;

        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];
        var pool = new List<string>(metNpcs.Count - 1);
        for (int i = 0; i < metNpcs.Count; i++)
        {
            if (!string.Equals(metNpcs[i], giver, StringComparison.OrdinalIgnoreCase))
                pool.Add(metNpcs[i]);
        }

        var picked = new List<string>(n);
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        string namesList = string.Join(", ", picked);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "TalkAll",
                Kind = AdventureStepKind.Talk,
                Targets = picked,
                Count = n,
                Description = ModEntry.I18n.Get("quest.social.checkOnFriends.step.talkAll", new { names = namesList })
            },
            new AdventureStepState
            {
                Name = "ReportToGiver",
                Kind = AdventureStepKind.Talk,
                Targets = new List<string> { giver },
                Requires = new List<string> { "TalkAll" },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.social.checkOnFriends.step.report", new { npc = giver })
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.social.checkOnFriends.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipIntermediate) },
            Title = ModEntry.I18n.Get("quest.social.checkOnFriends.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.social.checkOnFriends.description", new { npc = giver, names = namesList }),
            TargetMessage = ModEntry.I18n.Get("quest.social.checkOnFriends.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 7c: Gus's Festival Feast variants --------------------

    /// Spring 6 (Egg Festival prep). Gus is taste-testing dishes for the festival;
    /// player delivers spring-themed ingredients, gets a "sample" cooked dish back as
    /// reward. CSV row 30. Reward kind = `Dish` only (no Festival Bonus), so no
    /// dependency on the Phase 9 `FestivalBias` reward kind.
    private static QuestPosting? GusFestivalFeastSpring(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;

        var ingredient = PickSpringIngredient(ctx);
        if (ingredient == null)
            return null;
        var sampleDish = PickSpringSampleDish(ctx);
        if (sampleDish == null)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverIngredient",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { ingredient.QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusSpring.step.deliver", new { count = qty, item = ingredient.DisplayName })
            }
        }, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusSpring.targetMessage", new { dish = sampleDish.DisplayName }));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new ObjectReward(sampleDish.QualifiedItemId) },
            Title = ModEntry.I18n.Get("quest.festival.gusSpring.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusSpring.description", new { count = qty, item = ingredient.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusSpring.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Winter 18 (Winter Star prep). Ship winter-themed forageables; reward is
    /// FriendshipMultiSmall to every met villager (CSV row 33: "Only +friendship
    /// with NPCs farmer has already met."). Stacked FriendshipReward entries — one
    /// per met villager — get applied at completion via the existing reward pipeline.
    private static QuestPosting? GusFestivalFeastWinter(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;

        var winterForage = ctx.Items.GetForageItems("winter");
        if (winterForage.Count < 3)
            return null;

        // Sample 3 distinct winter forageables for parallel Ship steps.
        var pool = new List<ResolvedItem>(winterForage);
        var picks = new List<ResolvedItem>(3);
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            picks.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "ShipItem" + i,
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusWinter.step.ship", new { count = qty, item = picks[i].DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: "Gus");

        var posting = new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Title = ModEntry.I18n.Get("quest.festival.gusWinter.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusWinter.description", new
            {
                count = qty,
                item1 = picks.Count > 0 ? picks[0].DisplayName : string.Empty,
                item2 = picks.Count > 1 ? picks[1].DisplayName : string.Empty,
                item3 = picks.Count > 2 ? picks[2].DisplayName : string.Empty
            }),
            PreBuiltQuest = quest
        };

        // FriendshipMultiSmall to every met villager. Iterating
        // `Game1.player.friendshipData` covers vanilla + modded NPCs uniformly.
        foreach (var (name, _) in Game1.player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null || npc.IsMonster || !npc.IsVillager)
                continue;
            posting.Rewards.Add(new FriendshipReward(name, ctx.Config.FriendshipMultiSmall));
        }

        return posting;
    }

    /// Curated vanilla spring cooking ingredients. Modded ingredient pickup is a
    /// follow-up; for now we keep the pool focused on items the player can plausibly
    /// produce on the farm or forage by Spring 6 (the trigger date is early in the
    /// season so deep-game crops aren't reachable yet).
    private static readonly (string Id, string Name)[] SpringIngredientPool =
    {
        ("(O)20", "Leek"),
        ("(O)16", "Wild Horseradish"),
        ("(O)18", "Daffodil"),
        ("(O)22", "Dandelion"),
        ("(O)399", "Spring Onion")
    };

    /// Curated vanilla spring "sample" dishes Gus could plausibly hand back as a taste
    /// from his potluck testing. All have spring-themed ingredients in their vanilla
    /// recipes, so they read as in-character for the Egg Festival prep flavour.
    private static readonly (string Id, string Name)[] SpringSampleDishPool =
    {
        ("(O)196", "Salad"),
        ("(O)244", "Roots Platter"),
        ("(O)457", "Vegetable Medley"),
        ("(O)195", "Omelet")
    };

    private static ResolvedItem? PickSpringIngredient(QuestContext ctx)
    {
        var (id, _) = SpringIngredientPool[Game1.random.Next(SpringIngredientPool.Length)];
        return ctx.Items.TryResolveItem(id);
    }

    private static ResolvedItem? PickSpringSampleDish(QuestContext ctx)
    {
        var (id, _) = SpringSampleDishPool[Game1.random.Next(SpringSampleDishPool.Length)];
        return ctx.Items.TryResolveItem(id);
    }

    // -------------------- Phase 8a: Preserves Season (SpecialOrder) --------------------

    /// Vanilla artisan-good context tags. Each entry is one shippable category the order
    /// can ask for. The synthetic `id_o_<itemid>` tag (vanilla auto-generates these per
    /// `Utility.getStandardDescriptionFromItem`) lets us target a single specific item
    /// without needing the item to declare a custom context tag in `Data/Objects`.
    private static readonly (string Tag, string I18nKey)[] PreservesCategories =
    {
        ("jelly_item", "quest.fall.preservesSeason.category.jam"),
        ("pickle_item", "quest.fall.preservesSeason.category.pickle"),
        ("wine_item", "quest.fall.preservesSeason.category.wine"),
        ("id_o_driedmushrooms", "quest.fall.preservesSeason.category.driedMushroom")
    };

    /// CSV row 57. Fall 1, mail-deferred (delivered as a vanilla SpecialOrder so vanilla
    /// owns accept + objective tracking + reward grant). Single dispatched requester
    /// asks for `objectiveCount` distinct artisan-good categories at `qty` units each;
    /// objective count + qty both scale with Farming level when `DifficultyScaling` is on.
    /// Reward = approximate "above sell" gold bonus (computed across the requested units)
    /// + `FriendshipBasic` to the requester. `DeadlineExtended` → vanilla `Month` window.
    private static QuestPosting? PreservesSeason(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string requester = metNpcs[Game1.random.Next(metNpcs.Count)];

        bool scaling = ctx.Config.DifficultyScaling;
        int farming = Game1.player.FarmingLevel;
        // Objective count: 2 at level 0-2, 3 at 3-5, 4 at 6+. Off → all 4 categories.
        int objectiveCount = scaling
            ? Math.Clamp(2 + farming / 3, 2, PreservesCategories.Length)
            : PreservesCategories.Length;
        // Per-objective quantity: floor 3, +1 per farming level above 1. Off → fixed 8.
        int qty = scaling
            ? Math.Max(3, 2 + farming)
            : 8;

        // Sample objectiveCount distinct categories from the pool so the order doesn't
        // ask for the same category twice on a high-skill roll.
        var idx = new List<int>(PreservesCategories.Length);
        for (int i = 0; i < PreservesCategories.Length; i++)
            idx.Add(i);
        var picked = new List<(string Tag, string I18nKey)>(objectiveCount);
        for (int i = 0; i < objectiveCount; i++)
        {
            int p = Game1.random.Next(idx.Count);
            picked.Add(PreservesCategories[idx[p]]);
            idx.RemoveAt(p);
        }

        var objectives = new List<SpecialOrderObjectiveSpec>(picked.Count);
        var displayNames = new List<string>(picked.Count);
        foreach (var (tag, i18nKey) in picked)
        {
            string categoryName = ModEntry.I18n.Get(i18nKey);
            displayNames.Add(categoryName);
            objectives.Add(new SpecialOrderObjectiveSpec
            {
                Type = "Ship",
                Text = ModEntry.I18n.Get("quest.fall.preservesSeason.objective", new { count = qty, item = categoryName }),
                RequiredCount = qty,
                Data = { ["AcceptedContextTags"] = tag }
            });
        }

        // Money reward = approximate "above sell" bonus across the requested units. The
        // vanilla shipping bin already pays the player at full sell price; this is the
        // bonus on top, sized to feel like a meaningful reward at the relevant skill
        // band. Avg price ~200 gold matches the median sell price across the four
        // artisan-good categories at silver quality.
        const int approxAvgPrice = 200;
        int totalUnits = qty * picked.Count;
        int moneyBonus = (int)(approxAvgPrice * totalUnits * Math.Max(0f, ctx.Config.RewardMultiplierAboveSell - 1f));
        if (moneyBonus < 200)
            moneyBonus = 200;

        // Money stays in vanilla's Rewards path so the player gets the standard "click
        // reward box for X gold" UX. Friendship moves to FrameworkRewards because the
        // Special Order Adjustments content pack (a friendship-tuning mod) edits
        // Data/SpecialOrders to overwrite vanilla Friendship rewards globally; routing
        // through the framework path bypasses that interception entirely.
        var vanillaRewards = new List<SpecialOrderRewardSpec>
        {
            new()
            {
                Type = "Money",
                Data = { ["Amount"] = moneyBonus.ToString() }
            }
        };
        var frameworkRewards = new List<RewardSpec>
        {
            new FriendshipReward(requester, ctx.Config.FriendshipBasic)
        };

        string namesList = string.Join(", ", displayNames);

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Custom,
            Kind = PostingKind.SpecialOrder,
            QuestGiver = requester,
            SpecialOrder = new SpecialOrderSpec
            {
                Name = ModEntry.I18n.Get("quest.fall.preservesSeason.title", new { npc = requester }),
                Text = ModEntry.I18n.Get("quest.fall.preservesSeason.text", new { npc = requester, items = namesList, count = qty }),
                Requester = requester,
                Duration = "Month",
                Objectives = objectives,
                Rewards = vanillaRewards,
                FrameworkRewards = frameworkRewards
            }
        };
    }

    // -------------------- Phase 8c: Adventurer's Guild deep-dive quests --------------------

    /// Vanilla ore + stone ids accepted by both deep-dive quests' Deliver step. Stone is
    /// included per the CSV row's "(any type of ore)/stone" wording — Marlon's not picky
    /// about what fills the crate, the bar reward is fixed by quest difficulty rather
    /// than the player's specific haul.
    private static readonly string[] AnyOreOrStone =
    {
        "(O)378", // Copper Ore
        "(O)380", // Iron Ore
        "(O)384", // Gold Ore
        "(O)386", // Iridium Ore
        "(O)390"  // Stone
    };

    /// CSV row 67. Daily-board posting on the Adventurer's Guild custom board. Two-step
    /// Adventure: ReachLevel a target floor in Skull Cavern → ship the ore/stone haul via
    /// the farm shipping bin. Floor target rolls in [50, Config.SkullCavernMaxLevel]; haul
    /// size scales with Mining skill when DifficultyScaling is on. Reward = `GoldAdvancedBase`
    /// + an iridium-bar count proportional to the haul (5 ores ≈ 1 bar, vanilla smelt ratio),
    /// granted directly into inventory + money on quest completion.
    ///
    /// Shipping-step framing keeps the quest playable on vanilla saves: Marlon is only a
    /// shopkeeper sprite without SVE, so `OnItemOfferedToNpc` never fires for him. The
    /// shipping-bin observer (`AdventureQuest.ObserveShippingBin` at DayEnding) doesn't
    /// require an NPC at all.
    private static QuestPosting? SkullCavernDeepDive(QuestContext ctx)
    {
        // Gate on Skull Cavern access. Vanilla unlocks the Cavern after the first iridium
        // ore drops the player past floor 120; reflecting that in availability keeps the
        // quest from posting on saves where Skull Cavern is still locked.
        if (Game1.player.deepestMineLevel <= 120)
            return null;

        int maxFloor = Math.Max(20, ModEntry.Config.SkullCavernMaxLevel);
        int targetFloor = Game1.random.Next(50, maxFloor + 1);

        int haul = ctx.Config.DifficultyScaling
            ? Math.Max(15, 10 + 2 * Game1.player.MiningLevel)
            : 25;

        int bars = Math.Max(2, haul / 5);
        int gold = ctx.Config.GoldAdvancedBase;

        // Marlon is the in-character "giver" the journal references, but no NPC turn-in
        // happens — the Ship step closes the quest on its own at DayEnding. The bar
        // reward arrives the morning after via a parameterised mail key so the player
        // doesn't get items mysteriously appearing in inventory while sleeping.
        const string giver = "Marlon";
        const string barId = "337"; // Iridium Bar (bare id; the mail token uses bare ids)
        string letterKey = BuildDeepDiveRewardLetterKey(barId, bars);
        var oreIds = new List<string>(AnyOreOrStone);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ReachFloor",
                Kind = AdventureStepKind.ReachLevel,
                Targets = new List<string> { "SkullCavern" },
                Count = targetFloor,
                Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.step.reach", new { floor = targetFloor })
            },
            new AdventureStepState
            {
                Name = "ShipHaul",
                Kind = AdventureStepKind.Ship,
                Items = oreIds,
                Count = haul,
                Requires = new List<string> { "ReachFloor" },
                Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.step.ship", new { count = haul })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new MailReward(letterKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.title"),
            Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.description", new { floor = targetFloor, count = haul }),
            PreBuiltQuest = quest
        };
    }

    /// CSV row 78. Sibling of `SkullCavernDeepDive` for the regular Mines (capped at 120).
    /// Floor target rolls in a band that scales with Mining skill so a fresh save isn't
    /// asked to reach floor 120 immediately. Bar type reward matches the floor band:
    /// Copper at 1-79, Iron at 80-99, Gold at 100-120. Ship step at DayEnding closes the
    /// quest without requiring an NPC turn-in (vanilla Marlon isn't a giftable villager).
    private static QuestPosting? MinesDeepDive(QuestContext ctx)
    {
        int currentDeepest = Math.Min(120, Game1.player.deepestMineLevel);
        if (currentDeepest < 5)
            return null; // need at least a few floors of progress before asking for more

        // Target a floor band slightly past where the player has already been so the
        // quest demands genuine progress without being unreachable. Cap at 120.
        int low = Math.Max(20, currentDeepest - 30);
        int high = Math.Min(120, currentDeepest + 30);
        if (high <= low)
            high = low + 1;
        int targetFloor = Game1.random.Next(low, high + 1);

        int haul = ctx.Config.DifficultyScaling
            ? Math.Max(8, 6 + Game1.player.MiningLevel)
            : 15;

        // Bar reward type matches the floor band the quest targets — the Guild scales the
        // payout to whatever's plausible at that depth.
        (string barId, int barCount) = targetFloor switch
        {
            >= 100 => ("336", Math.Max(1, haul / 5)), // Gold Bar
            >= 80 => ("335", Math.Max(1, haul / 5)),  // Iron Bar
            _ => ("334", Math.Max(1, haul / 5))        // Copper Bar
        };
        int gold = ctx.Config.GoldIntermediateBase;
        string letterKey = BuildDeepDiveRewardLetterKey(barId, barCount);

        const string giver = "Marlon";
        var oreIds = new List<string>(AnyOreOrStone);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ReachFloor",
                Kind = AdventureStepKind.ReachLevel,
                Targets = new List<string> { "Mine" },
                Count = targetFloor,
                Description = ModEntry.I18n.Get("quest.mining.minesDeepDive.step.reach", new { floor = targetFloor })
            },
            new AdventureStepState
            {
                Name = "ShipHaul",
                Kind = AdventureStepKind.Ship,
                Items = oreIds,
                Count = haul,
                Requires = new List<string> { "ReachFloor" },
                Description = ModEntry.I18n.Get("quest.mining.minesDeepDive.step.ship", new { count = haul })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new MailReward(letterKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.mining.minesDeepDive.title"),
            Description = ModEntry.I18n.Get("quest.mining.minesDeepDive.description", new { floor = targetFloor, count = haul }),
            PreBuiltQuest = quest
        };
    }

    /// Builds a parameterised reward letter key that bakes the bar id + count into the
    /// key itself. The content mod's `Data/mail` asset edit parses the suffix on the fly,
    /// so neither the framework nor save state needs to track per-quest reward bodies.
    /// Format: `RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}` — both numeric so the
    /// `%item object {barId} {count} %%` mail token slots in directly.
    internal static string BuildDeepDiveRewardLetterKey(string barId, int count)
        => $"RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}";

    /// CSV row 54. Daily-board bulk-crop delivery for Pierre's General Store. Picks 3
    /// distinct seasonal crops; player delivers a per-crop quantity that scales with
    /// Farming skill. Reward = `ShopDiscount` on Pierre's `SeedShop` for the matching
    /// seed ids, lasting `SeedShopDiscountDurationDays` in-game days at
    /// `SeedShopDiscountPercent` off.
    private static QuestPosting? PierresStockUp(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count < 3)
            return null;

        // Sample 3 distinct seasonal crops; smaller pools fall back to whatever the
        // season has (we already early-return on Count < 3 above).
        var pool = new List<ResolvedItem>(crops);
        var picks = new List<ResolvedItem>(3);
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            picks.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        int qtyPer = ctx.Config.DifficultyScaling
            ? Math.Max(6, 4 + 2 * Game1.player.FarmingLevel)
            : 12;

        const string giver = "Pierre";

        // Reverse-map each picked crop's harvest item id to its seed id so the discount
        // applies specifically to the seeds Pierre stocks for the requested crops. Misses
        // (modded crops with no SeedShop entry) just don't get discounted; the whole
        // quest still posts.
        var seedIds = new List<string>(picks.Count);
        foreach (var crop in picks)
        {
            string? seedId = ResolveSeedIdForHarvest(ctx, crop.QualifiedItemId);
            if (!string.IsNullOrEmpty(seedId))
                seedIds.Add(seedId!);
        }

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "DeliverCrop" + i,
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { giver },
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qtyPer,
                Description = ModEntry.I18n.Get("quest.farming.pierresStockUp.step.deliver", new { count = qtyPer, item = picks[i].DisplayName, npc = giver })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.farming.pierresStockUp.targetMessage"));

        var posting = new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Title = ModEntry.I18n.Get("quest.farming.pierresStockUp.title"),
            Description = ModEntry.I18n.Get("quest.farming.pierresStockUp.description", new
            {
                count = qtyPer,
                item1 = picks.Count > 0 ? picks[0].DisplayName : string.Empty,
                item2 = picks.Count > 1 ? picks[1].DisplayName : string.Empty,
                item3 = picks.Count > 2 ? picks[2].DisplayName : string.Empty
            }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.pierresStockUp.targetMessage"),
            PreBuiltQuest = quest
        };

        if (ModEntry.Config.SeedShopDiscountPercent > 0 && ModEntry.Config.SeedShopDiscountDurationDays > 0)
        {
            // Half the requested haul as the per-visit seed cap on quest-injected
            // entries. Pierre stocks `qtyPer / 2` per missing seed (minimum 2) so a
            // 16-crop request lands as 8 seeds at the discount — enough to grow back a
            // comparable harvest without making the discount window an unlimited
            // farming press.
            int guaranteedStock = Math.Max(2, qtyPer / 2);
            posting.Rewards.Add(new ShopDiscountReward(
                ShopId: "SeedShop",
                PercentOff: ModEntry.Config.SeedShopDiscountPercent,
                DurationDays: ModEntry.Config.SeedShopDiscountDurationDays,
                AppliesTo: seedIds.Count > 0 ? seedIds : null,
                GuaranteedStock: guaranteedStock));
        }

        return posting;
    }

    /// Walks `Data/Crops` for a row whose `HarvestItemId` matches the requested crop, and
    /// returns the qualified seed id (the dictionary key is the seed's bare item id). Used
    /// to scope Pierre's seed-shop discount to the seeds for the quested crops.
    private static string? ResolveSeedIdForHarvest(QuestContext ctx, string harvestQualifiedId)
    {
        if (string.IsNullOrEmpty(harvestQualifiedId))
            return null;
        string bareHarvest = harvestQualifiedId.StartsWith("(O)", StringComparison.Ordinal)
            ? harvestQualifiedId[3..]
            : harvestQualifiedId;
        foreach (var (seedId, data) in ctx.Data.Crops)
        {
            if (string.Equals(data.HarvestItemId, bareHarvest, StringComparison.OrdinalIgnoreCase))
                return "(O)" + seedId;
        }
        return null;
    }

    // -------------------- Phase 9a: Massive Harvest Request --------------------

    /// CSV row 48. Daily-board single-objective Ship quest. Morris (JojaMart manager)
    /// asks the farmer to dump `qty` of one seasonal crop into the farm shipping bin;
    /// vanilla sells the items normally and the framework's DayEnding observer credits
    /// the count. Reward = `RewardMultiplierBelowSell` of the crop's price (Joja pays
    /// below sell so the headline gold figure looks high while the crop's lost-value
    /// brings it back near break-even).
    ///
    /// Tier 1 consequence keyed off `Data/NPCGiftTastes`: villagers who love the chosen
    /// crop comment positively + gain `+FriendshipBasic`; villagers who hate it
    /// comment negatively + lose `-FriendshipBasic`. Lines are pre-resolved against
    /// the content mod's i18n at build-time so the persisted dialogue queue holds
    /// plain strings.
    private static QuestPosting? MassiveHarvestRequest(QuestContext ctx)
    {
        if (Game1.player.FarmingLevel < 7)
            return null; // CSV labels this Expert (Skill 10); start surfacing it once the
                         // farmer is plausibly in the late-game band.

        // JojaCorpRep dispatcher pool covers both vanilla Morris and SVE's MorrisTod.
        // Returns null on Community-Center-route saves where neither character exists.
        string? giver = ctx.Dispatch.Pick(DispatchRoles.JojaCorpRep);
        if (giver == null)
            return null;

        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;

        var crop = crops[Game1.random.Next(crops.Count)];
        int qty = Math.Max(ModEntry.Config.CropMassiveQty, 30 + 5 * Game1.player.FarmingLevel);
        int basePrice = Math.Max(crop.SellPrice, 30);
        int gold = (int)(basePrice * qty * ctx.Config.RewardMultiplierBelowSell);

        var consequence = ModEntry.Config.ConsequencesEnabled
            ? new ConsequenceSpec
            {
                Tier = ConsequenceTier.Tier1,
                Source = ConsequenceSource.GiftTastes,
                Subject = crop.QualifiedItemId,
                LovedLine = ModEntry.I18n.Get("quest.farming.massiveHarvest.consequence.loved", new { item = crop.DisplayName }),
                HatedLine = ModEntry.I18n.Get("quest.farming.massiveHarvest.consequence.hated", new { item = crop.DisplayName, npc = giver })
            }
            : null;

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Expert,
            QuestType = BoardQuestType.Ship,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.farming.massiveHarvest.title"),
            Description = ModEntry.I18n.Get("quest.farming.massiveHarvest.description", new { npc = giver, qty, item = crop.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.farming.massiveHarvest.objective", new { qty, item = crop.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.massiveHarvest.targetMessage")
        };
    }

    // -------------------- Phase 9b: saloon weekly specials + Grand Feast --------------------

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
    private static QuestPosting? MediumFishingHaul(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.BulkFishBuyer);
        if (giver == null)
            return null;

        var fish = ctx.Config.FishingIgnoresVisitedLocations
            ? ctx.Items.GetSeasonalFish(ctx.Season)
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season);
        if (fish.Count == 0)
            return null;

        var target = fish[Game1.random.Next(fish.Count)];
        int qty = Math.Max(1, ModEntry.Config.FishHaulMediumQty);
        int basePrice = Math.Max(target.SellPrice, 30);
        int gold = (int)(basePrice * qty * ctx.Config.RewardMultiplierBelowSell);

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            var ecology = ResolveEcologyTargets(ctx, includeLinus: false, exclude: giver);
            if (ecology.Count > 0)
            {
                consequence = new ConsequenceSpec
                {
                    Tier = ConsequenceTier.Tier2,
                    Source = ConsequenceSource.Static,
                    Targets = ecology,
                    HatedLine = ModEntry.I18n.Get(
                        "quest.fishing.mediumHaul.consequence.hated",
                        new { item = target.DisplayName, npc = giver })
                };
            }
        }

        string flavour = string.Equals(giver, "Pierre", StringComparison.OrdinalIgnoreCase)
            ? ModEntry.I18n.Get("quest.fishing.mediumHaul.description.pierre", new { qty, item = target.DisplayName })
            : ModEntry.I18n.Get("quest.fishing.mediumHaul.description.joja", new { qty, item = target.DisplayName, npc = giver });

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.fishing.mediumHaul.title", new { npc = giver }),
            Description = flavour,
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.mediumHaul.objective", new { qty, item = target.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.mediumHaul.targetMessage")
        };
    }

    /// CSV row 65. Daily-board fishing quest. SaloonChef-pool giver (Gus / Pika RSV /
    /// Rosa ESV / Celestine VMV) asks for a large haul of one edible non-poisonous fish;
    /// reward is `RewardMultiplierFishPremium` of the fish's price × qty. Tier 3 ecology
    /// chain consequence: every ecology NPC present on the save plus Linus loses
    /// `FriendshipLarge` worth of friendship spread evenly over `ChainDays` days, with
    /// one chained dialogue line per day. Static source so the engine pushes a chain to
    /// every target rather than sampling a single NPC.
    private static QuestPosting? SeafoodNight(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        var fish = ctx.Config.FishingIgnoresVisitedLocations
            ? ctx.Items.GetSeasonalFish(ctx.Season)
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season);
        var pool = fish.Where(IsEdibleNonPoisonous).ToList();
        if (pool.Count == 0)
            return null;

        var target = pool[Game1.random.Next(pool.Count)];
        int qty = Math.Max(1, ModEntry.Config.FishHaulLargeQty);
        int basePrice = Math.Max(target.SellPrice, 30);
        int gold = (int)(basePrice * qty * ctx.Config.RewardMultiplierFishPremium);

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            var ecology = ResolveEcologyTargets(ctx, includeLinus: true, exclude: giver);
            if (ecology.Count > 0)
            {
                consequence = new ConsequenceSpec
                {
                    Tier = ConsequenceTier.Tier3,
                    Source = ConsequenceSource.Static,
                    Targets = ecology,
                    ChainDays = 3,
                    ChainLines = new List<string>
                    {
                        ModEntry.I18n.Get("quest.fishing.seafoodNight.consequence.chain.day1", new { item = target.DisplayName, npc = giver }),
                        ModEntry.I18n.Get("quest.fishing.seafoodNight.consequence.chain.day2", new { item = target.DisplayName, npc = giver }),
                        ModEntry.I18n.Get("quest.fishing.seafoodNight.consequence.chain.day3", new { item = target.DisplayName, npc = giver })
                    }
                };
            }
        }

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Expert,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.fishing.seafoodNight.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.fishing.seafoodNight.description", new { qty, item = target.DisplayName, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.seafoodNight.objective", new { qty, item = target.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.seafoodNight.targetMessage")
        };
    }

    /// CSV row 53. Daily-board item delivery. Picks a buyer from the `MonsterPartsBuyer`
    /// dispatch pool (Wizard + Abigail vanilla; Lance + MarlonFay SVE; Mr. Aguar RSV;
    /// Eli ESV; Maryam VMV) and asks for a quantity of one rare monster drop (Bat Wing
    /// / Solar Essence / Void Essence / Bug Meat). Reward = a stack of one random gem
    /// scaled to clear `GoldIntermediateBase`. Tier 1 negative consequence routed via
    /// `Source: Static` to Krobus / Sen (East Scarp) / Dwarf — friends of the underground
    /// don't appreciate the trade.
    private static QuestPosting? MonsterParts(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.MonsterPartsBuyer);
        if (giver == null)
            return null;

        var drop = MonsterDropPool[Game1.random.Next(MonsterDropPool.Length)];
        var resolved = ctx.Items.TryResolveItem(drop.Id);
        if (resolved == null)
            return null;
        int qty = Game1.random.Next(5, 11);

        var gem = MonsterPartsGemRewards[Game1.random.Next(MonsterPartsGemRewards.Length)];

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            var underground = ResolveUndergroundTargets(exclude: giver);
            if (underground.Count > 0)
            {
                consequence = new ConsequenceSpec
                {
                    Tier = ConsequenceTier.Tier1,
                    Source = ConsequenceSource.Static,
                    Targets = underground,
                    HatedLine = ModEntry.I18n.Get(
                        "quest.mining.monsterParts.consequence.hated",
                        new { item = resolved.DisplayName, npc = giver })
                };
            }
        }

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = resolved.QualifiedItemId,
            ObjectiveItemName = resolved.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new ObjectReward(gem.Id, gem.Count) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.mining.monsterParts.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.mining.monsterParts.description", new { qty, item = resolved.DisplayName, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.monsterParts.objective", new { qty, item = resolved.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.monsterParts.targetMessage")
        };
    }

    /// Vanilla "rare" monster drops. Modded drops aren't enumerable without per-mod data,
    /// so the pool stays vanilla — the framework's `Data/NPCGiftTastes` consequence path
    /// still picks up modded NPCs who happen to like or hate any of these.
    private static readonly (string Id, string Name)[] MonsterDropPool =
    {
        ("(O)767", "Bat Wing"),
        ("(O)768", "Solar Essence"),
        ("(O)769", "Void Essence"),
        ("(O)684", "Bug Meat")
    };

    /// Gem reward pool sized so the headline value clears `GoldIntermediateBase` (~500g).
    /// Vanilla sell prices: Diamond 750, Ruby 250 (×3 = 750), Emerald 250 (×3 = 750),
    /// Topaz 80 (×7 = 560), Jade 200 (×3 = 600), Aquamarine 180 (×3 = 540), Amethyst 100
    /// (×6 = 600). All comfortably above the GoldIntermediateBase floor.
    private static readonly (string Id, int Count)[] MonsterPartsGemRewards =
    {
        ("(O)72", 1),  // Diamond
        ("(O)64", 3),  // Ruby
        ("(O)60", 3),  // Emerald
        ("(O)68", 7),  // Topaz
        ("(O)70", 3),  // Jade
        ("(O)62", 3),  // Aquamarine
        ("(O)66", 6)   // Amethyst
    };

    /// Pufferfish carries the Nausea status effect when eaten — the only vanilla "fish"
    /// any reasonable cook would call poisonous. Filtered out of the Seafood Night pool
    /// so the CSV's "edible non-poisonous" framing holds. Modded fish stay in as long as
    /// their Edibility is positive.
    private static readonly HashSet<string> SeafoodNightExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "(O)128" // Pufferfish
    };

    private static bool IsEdibleNonPoisonous(ResolvedItem fish)
    {
        if (SeafoodNightExclusions.Contains(fish.QualifiedItemId))
            return false;
        var data = StardewValley.ItemRegistry.GetData(fish.QualifiedItemId);
        if (data?.RawData is StardewValley.GameData.Objects.ObjectData obj)
            return obj.Edibility > 0;
        return false;
    }

    /// Resolves the live `EcologyMinded` pool (filtered by mod presence + NPC existence)
    /// and optionally appends Linus for Tier 3 quests. Excludes the quest giver — a
    /// shopkeeper who's also coincidentally in the ecology role shouldn't shame
    /// themselves on the next chat. The list is the resolved snapshot, so saves where a
    /// modded NPC is missing simply drop that entry.
    private static List<string> ResolveEcologyTargets(QuestContext ctx, bool includeLinus, string exclude)
    {
        var pool = ctx.Dispatch.ResolvePool(DispatchRoles.EcologyMinded);
        var targets = new List<string>(pool.Count + 1);
        foreach (var npc in pool)
        {
            if (string.Equals(npc, exclude, StringComparison.OrdinalIgnoreCase))
                continue;
            targets.Add(npc);
        }
        if (includeLinus
            && Game1.getCharacterFromName("Linus") != null
            && !targets.Any(n => string.Equals(n, "Linus", StringComparison.OrdinalIgnoreCase))
            && !string.Equals(exclude, "Linus", StringComparison.OrdinalIgnoreCase))
        {
            targets.Add("Linus");
        }
        return targets;
    }

    /// Krobus + Dwarf are vanilla; Sen ships with East Scarp. The list is the literal
    /// CSV row 53 set; the engine filters to met villagers downstream so unknown NPCs
    /// silently drop out. We still pre-filter by `getCharacterFromName` so we never queue
    /// a line for an NPC whose mod isn't loaded.
    private static List<string> ResolveUndergroundTargets(string exclude)
    {
        string[] candidates = { "Krobus", "Dwarf", "Sen" };
        var targets = new List<string>(candidates.Length);
        foreach (var npc in candidates)
        {
            if (string.Equals(npc, exclude, StringComparison.OrdinalIgnoreCase))
                continue;
            if (Game1.getCharacterFromName(npc) == null)
                continue;
            targets.Add(npc);
        }
        return targets;
    }

    // -------------------- Phase 9d: Forage with Linus --------------------

    /// CSV row 27. Daily-board, Linus giver, single-step `GiftUniqueNpcs` objective: gift a
    /// forage-category item that the recipient loves or likes to 5 distinct NPCs. Reward is
    /// `FriendshipLarge` with Linus only — no consequence engine wiring needed since the
    /// gift recipients already get the standard friendship boost from vanilla's gift flow.
    /// Quest gates on Linus being met (no point posting a "deliver gifts on Linus's behalf"
    /// quest before the player has met Linus).
    private static QuestPosting? ForageWithLinus(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Linus") == null)
            return null;

        const int recipientCount = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "GiftForage",
                Kind = AdventureStepKind.GiftUniqueNpcs,
                // Empty Targets = any villager qualifies. The handler enforces the
                // "loved or liked by recipient" + "$forage tagged item" filter at gift-time.
                Items = new List<string> { "$forage" },
                Count = recipientCount,
                Description = ModEntry.I18n.Get("quest.foraging.forageWithLinus.step.gift", new { count = recipientCount })
            }
        }, giver: "Linus");

        return new QuestPosting
        {
            Category = QuestCategory.Foraging,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Linus",
            ObjectiveQuantity = recipientCount,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new FriendshipReward("Linus", ctx.Config.FriendshipLarge) },
            Title = ModEntry.I18n.Get("quest.foraging.forageWithLinus.title"),
            Description = ModEntry.I18n.Get("quest.foraging.forageWithLinus.description", new { count = recipientCount }),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 9d: Gus's Festival Feasts (Fall + Summer) --------------------

    /// Curated vanilla fall ingredients. CSV row 31 calls for a "large" delivery; we keep
    /// the pool focused on items the player can plausibly produce or forage by Fall 8.
    private static readonly (string Id, string Name)[] FallIngredientPool =
    {
        ("(O)24", "Parsnip"),
        ("(O)266", "Red Cabbage"),
        ("(O)272", "Eggplant"),
        ("(O)270", "Corn"),
        ("(O)276", "Pumpkin"),
        ("(O)278", "Bok Choy"),
        ("(O)408", "Hazelnut"),
        ("(O)404", "Common Mushroom")
    };

    /// Vanilla fall-themed sample dishes Gus could plausibly hand back from his Fair
    /// taste-testing. Picked for ingredients in the fall pool above.
    private static readonly (string Id, string Name)[] FallSampleDishPool =
    {
        ("(O)205", "Fried Mushroom"),
        ("(O)225", "Fried Eel"),
        ("(O)240", "Farmer's Lunch"),
        ("(O)244", "Roots Platter"),
        ("(O)457", "Vegetable Medley"),
        ("(O)607", "Roasted Hazelnuts"),
        ("(O)608", "Pumpkin Pie")
    };

    /// Vanilla summer-themed dishes — ingredients that can be sourced by Summer 8 on a
    /// first-year save. Tighter than Fall because the trigger is earlier in the season.
    private static readonly (string Id, string Name)[] SummerIngredientPool =
    {
        ("(O)190", "Cauliflower"),
        ("(O)20", "Leek"),
        ("(O)188", "Green Bean"),
        ("(O)24", "Parsnip"),
        ("(O)252", "Rhubarb"),
        ("(O)254", "Melon"),
        ("(O)256", "Tomato"),
        ("(O)258", "Blueberry"),
        ("(O)260", "Hot Pepper"),
        ("(O)262", "Wheat"),
        ("(O)16", "Wild Horseradish")
    };

    /// Fall 8 prep for the Stardew Valley Fair. CSV row 31. Multi-step Adventure: deliver
    /// `GusFestivalFeastIngredientCount` distinct fall ingredients to Gus. Reward = a sample
    /// dish via `ObjectReward` plus a `FestivalBias` Fair magnitude — the bias bumps the
    /// player's grange score on Fall 16. Tier = Intermediate per the CSV.
    private static QuestPosting? GusFestivalFeastFall(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        int ingredientCount = ModEntry.Config.GusFestivalFeastIngredientCount;
        var picks = PickDistinctIngredients(ctx, FallIngredientPool, ingredientCount);
        if (picks.Count == 0)
            return null;
        var sampleDish = PickSample(ctx, FallSampleDishPool);
        if (sampleDish == null)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "DeliverIngredient" + i,
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusFall.step.deliver", new { count = qty, item = picks[i].DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusFall.targetMessage", new { dish = sampleDish.DisplayName }));

        string ingredientList = string.Join(", ", picks.Select(p => p.DisplayName));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new ObjectReward(sampleDish.QualifiedItemId),
                new FestivalBiasReward(FestivalKind.Fair, ModEntry.Config.FestivalBiasFairMagnitude)
            },
            Title = ModEntry.I18n.Get("quest.festival.gusFall.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusFall.description", new { count = qty, ingredients = ingredientList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusFall.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Summer 8 prep for the Luau (Summer 11). CSV row 32. Multi-step Adventure: deliver 3
    /// summer/spring-themed ingredients to Gus. Reward = `FestivalBias` Luau magnitude only
    /// — no sample dish, since the CSV explicitly calls out "Festival Bonus" as the only
    /// reward kind (a higher base potluck score). Tier = Intermediate.
    private static QuestPosting? GusFestivalFeastSummer(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        int ingredientCount = ModEntry.Config.GusFestivalFeastIngredientCount;
        var picks = PickDistinctIngredients(ctx, SummerIngredientPool, ingredientCount);
        if (picks.Count == 0)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "DeliverIngredient" + i,
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusSummer.step.deliver", new { count = qty, item = picks[i].DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusSummer.targetMessage"));

        string ingredientList = string.Join(", ", picks.Select(p => p.DisplayName));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FestivalBiasReward(FestivalKind.Luau, ModEntry.Config.FestivalBiasLuauMagnitude)
            },
            Title = ModEntry.I18n.Get("quest.festival.gusSummer.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusSummer.description", new { count = qty, ingredients = ingredientList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusSummer.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Picks up to `count` distinct entries from `pool`, dropping any whose id doesn't
    /// resolve (modded mismatch, Game1 not yet ready). Returns the resolved items in pick
    /// order. May return fewer than `count` when the pool can't satisfy that many resolves.
    private static List<ResolvedItem> PickDistinctIngredients(QuestContext ctx, (string Id, string Name)[] pool, int count)
    {
        var picks = new List<ResolvedItem>(count);
        var indices = new List<int>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
            indices.Add(i);
        for (int i = 0; i < count && indices.Count > 0; i++)
        {
            int j = Game1.random.Next(indices.Count);
            int poolIdx = indices[j];
            indices.RemoveAt(j);
            var resolved = ctx.Items.TryResolveItem(pool[poolIdx].Id);
            if (resolved != null)
                picks.Add(resolved);
            else
                i--; // try another
        }
        return picks;
    }

    private static ResolvedItem? PickSample(QuestContext ctx, (string Id, string Name)[] pool)
    {
        var (id, _) = pool[Game1.random.Next(pool.Length)];
        return ctx.Items.TryResolveItem(id);
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

    // -------------------- Phase 9.5a: Declarative quick-wins --------------------

    /// CSV row 28. Daily-board ItemDelivery. Anonymous board posting from one met NPC
    /// asking the player to deliver a small gift to a different met NPC. Item is picked
    /// from the recipient's loved or liked list so the gift always lands well. Reward =
    /// `RewardMultiplierBelowSell` × the item's sell price (the giver "covers the cost
    /// minus a finder's fee") plus `FriendshipBasic` with the recipient.
    private static QuestPosting? GiftDelivery(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count < 2)
            return null;

        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];
        var recipientPool = new List<string>(metNpcs.Count - 1);
        foreach (var npc in metNpcs)
            if (!string.Equals(npc, giver, StringComparison.OrdinalIgnoreCase))
                recipientPool.Add(npc);
        if (recipientPool.Count == 0)
            return null;
        string recipient = recipientPool[Game1.random.Next(recipientPool.Count)];

        var pick = PickLovedOrLikedItem(ctx, recipient);
        if (pick == null)
            return null;

        int basePrice = Math.Max(pick.SellPrice, 30);
        int gold = Math.Max(50, (int)(basePrice * ctx.Config.RewardMultiplierBelowSell));

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(recipient, ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.social.giftDelivery.title", new { recipient }),
            Description = ModEntry.I18n.Get("quest.social.giftDelivery.description", new { giver, recipient, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.social.giftDelivery.objective", new { item = pick.DisplayName, recipient }),
            TargetMessage = ModEntry.I18n.Get("quest.social.giftDelivery.targetMessage")
        };
    }

    /// CSV row 37. Summer-only daily-board ItemDelivery. Cold-tag items (vanilla and
    /// modded) for Harvey or Paula (RSV via the `HeatWaveRelief` dispatch role). Reward =
    /// a random clinic-themed item from the curated pool — Harvey runs a clinic, not a
    /// shop, so the "Harvey's shop" reward column is sourced from medicine-adjacent
    /// vanilla items.
    private static readonly (string Id, string Name)[] HeatWaveItemPool =
    {
        ("(O)253", "Triple Shot Espresso"),
        ("(O)395", "Coffee"),
        ("(O)167", "Joja Cola"),
        ("(O)773", "Life Elixir"),
        ("(O)772", "Oil of Garlic")
    };

    private static QuestPosting? HeatWaveRelief(QuestContext ctx)
    {
        if (!string.Equals(ctx.Season, "summer", StringComparison.OrdinalIgnoreCase))
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.HeatWaveRelief);
        if (giver == null)
            return null;

        var coldItems = ResolveColdTaggedItems(ctx);
        if (coldItems.Count == 0)
            return null;
        var pick = coldItems[Game1.random.Next(coldItems.Count)];

        int qty = Game1.random.Next(3, 6);

        var rewardItem = PickResolved(ctx, HeatWaveItemPool);
        var rewards = new List<RewardSpec>
        {
            new FriendshipReward(giver, ctx.Config.FriendshipBasic)
        };
        if (rewardItem != null)
            rewards.Add(new ObjectReward(rewardItem.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.targetMessage")
        };
    }

    /// Curated vanilla beach forageables. Mirrors the BeachForage pool used by
    /// BeachCleanup since both quests target the same kind of items.
    private static readonly (string Id, string Name)[] OceanForagePool =
    {
        ("(O)393", "Coral"),
        ("(O)397", "Sea Urchin"),
        ("(O)392", "Nautilus Shell"),
        ("(O)394", "Rainbow Shell"),
        ("(O)372", "Clam"),
        ("(O)718", "Cockle"),
        ("(O)719", "Mussel"),
        ("(O)723", "Oyster")
    };

    /// CSV row 38. Summer-only daily-board ItemDelivery. Picks an ecology-role giver
    /// (Demetrius / Maddie RSV / Mr. Aguar RSV / Dylan ESV — the existing `EcologyMinded`
    /// pool already matches the CSV's giver list) and asks for a small haul of one ocean
    /// forageable. Reward = `FriendshipBasic` plus a randomly-picked loved or liked item
    /// from the giver's own gift tastes ("they trade you something they'd enjoy").
    private static QuestPosting? JellyfishWatchPrep(QuestContext ctx)
    {
        if (!string.Equals(ctx.Season, "summer", StringComparison.OrdinalIgnoreCase))
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        var pick = PickResolved(ctx, OceanForagePool);
        if (pick == null)
            return null;
        int qty = Game1.random.Next(3, 6);

        var rewards = new List<RewardSpec>
        {
            new FriendshipReward(giver, ctx.Config.FriendshipBasic)
        };
        var lovedItem = PickLovedOrLikedItem(ctx, giver);
        if (lovedItem != null)
            rewards.Add(new ObjectReward(lovedItem.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.targetMessage")
        };
    }

    /// CSV row 50. Winter 13 (Night Market middle day). Picks a met NPC to send the
    /// player on a non-current-season seed restock. Filter walks `Data/Crops` and keeps
    /// any seed whose `Seasons` list excludes Winter so the request reads as "stock up
    /// for next year while the Night Market's Magic Boat is in town". Reward =
    /// `FriendshipBasic` with the picked NPC.
    private static QuestPosting? MerchantUnpacking(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var seeds = ResolveNonWinterSeeds(ctx);
        if (seeds.Count == 0)
            return null;
        var pick = seeds[Game1.random.Next(seeds.Count)];

        int qty = Game1.random.Next(3, 7);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Ship,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.festival.merchantUnpacking.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.festival.merchantUnpacking.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.merchantUnpacking.objective", new { qty, item = pick.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.merchantUnpacking.targetMessage")
        };
    }

    /// CSV row 52. Daily-board SlayMonster (any monster). Marlon dispatches the request
    /// from the Adventurer's Guild. Reward = `GoldIntermediateBase` + one random item
    /// from the framework's combat-food pool (seeded by this content mod with vanilla
    /// combat-buff foods at `RegistrationOpen`; consumer mods can extend through
    /// `IMoreQuestsApi.RegisterCombatFood`).
    private static QuestPosting? MonsterHunt(QuestContext ctx)
    {
        if (Game1.player.deepestMineLevel < 1)
            return null;

        const string giver = "Marlon";
        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(8, 6 + 2 * Game1.player.CombatLevel)
            : 12;

        int gold = ctx.Config.GoldIntermediateBase;

        var pool = ModEntry.Framework?.GetCombatFoodPool() ?? Array.Empty<string>();
        ResolvedItem? rewardFood = null;
        if (pool.Count > 0)
        {
            // Try a few times so a missing modded id doesn't kill the reward.
            for (int i = 0; i < Math.Min(pool.Count, 5) && rewardFood == null; i++)
                rewardFood = ctx.Items.TryResolveItem(pool[Game1.random.Next(pool.Count)]);
        }

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        if (rewardFood != null)
            rewards.Add(new ObjectReward(rewardFood.QualifiedItemId));

        var quest = new AnyMonsterQuest
        {
            target = { Value = giver },
            monsterName = { Value = "any" },
            numberToKill = { Value = qty },
            reward = { Value = gold },
            targetMessage = ModEntry.I18n.Get("quest.mining.monsterHunt.targetMessage")
        };

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = giver,
            ObjectiveItemId = "any monster",
            ObjectiveItemName = "any monster",
            ObjectiveQuantity = qty,
            TargetMonster = "any",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.mining.monsterHunt.title"),
            Description = ModEntry.I18n.Get("quest.mining.monsterHunt.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.monsterHunt.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.monsterHunt.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Vanilla rare forageables. Modded forage gets folded in via the `forage_item`
    /// context tag so this list is the seed; the resolver appends matching modded ids.
    private static readonly (string Id, string Name)[] RareForagePool =
    {
        ("(O)394", "Rainbow Shell"),
        ("(O)88", "Cactus Fruit"),
        ("(O)851", "Magma Cap")
    };

    /// CSV row 62. Daily-board ItemDelivery. Picks a met NPC to take a small stack of
    /// rare forage. Reward = `GoldIntermediateBase` + 10 of one current-season seed
    /// (so the player gets some farming material instead of a random gold lump).
    private static QuestPosting? RareForageHunt(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var pool = ResolveRareForage(ctx);
        if (pool.Count == 0)
            return null;
        var pick = pool[Game1.random.Next(pool.Count)];

        int qty = Game1.random.Next(2, 5);
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var seedReward = PickSeasonalSeed(ctx);
        if (seedReward != null)
            rewards.Add(new ObjectReward(seedReward.QualifiedItemId, 10));

        return new QuestPosting
        {
            Category = QuestCategory.Foraging,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.foraging.rareForage.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.foraging.rareForage.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.foraging.rareForage.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.foraging.rareForage.targetMessage")
        };
    }

    /// Iridium Bar + vanilla rare gems. The objective rolls one entry; the reward is
    /// independent and can be either Artifact Troves or a stack of the same gem family.
    private static readonly (string Id, string Name)[] RareMaterialPool =
    {
        ("(O)337", "Iridium Bar"),
        ("(O)72", "Diamond"),
        ("(O)64", "Ruby"),
        ("(O)60", "Emerald"),
        ("(O)70", "Jade"),
        ("(O)62", "Aquamarine"),
        ("(O)68", "Topaz"),
        ("(O)66", "Amethyst")
    };

    /// CSV row 63. Daily-board ItemDelivery to Clint. Asks for 1-3 Iridium Bars or 3-5
    /// rare gems. Reward = `GoldAdvancedBase` + either 3 Artifact Troves or 5 random
    /// gems (rolled per-fire). Skill gates on Mining 7 so the quest only surfaces when
    /// the player can plausibly source the requested materials.
    private static QuestPosting? RareMaterialRequest(QuestContext ctx)
    {
        if (Game1.player.MiningLevel < 7)
            return null;

        const string giver = "Clint";
        var pick = PickResolved(ctx, RareMaterialPool);
        if (pick == null)
            return null;

        bool isBar = pick.QualifiedItemId == "(O)337";
        int qty = isBar ? Game1.random.Next(1, 4) : Game1.random.Next(3, 6);
        int gold = ctx.Config.GoldAdvancedBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        if (Game1.random.NextDouble() < 0.5)
        {
            // 3 Artifact Troves
            rewards.Add(new ObjectReward("(O)275", 3));
        }
        else
        {
            // 5 of one randomly-picked gem (skip the bar entry from the pool)
            var gem = RareMaterialPool[Game1.random.Next(1, RareMaterialPool.Length)];
            rewards.Add(new ObjectReward(gem.Id, 5));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.mining.rareMaterial.title"),
            Description = ModEntry.I18n.Get("quest.mining.rareMaterial.description", new { qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.rareMaterial.objective", new { qty, item = pick.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.rareMaterial.targetMessage")
        };
    }

    /// CSV row 66. Winter 22 DateLocked. Resolves the player's Winter Star recipient via
    /// `Utility.GetRandomWinterStarParticipant` (the same deterministic random the game
    /// uses to assign secret-santa pairings) and embeds a hint about the recipient's
    /// loved gifts. Player can opt out via `SecretGiftHintEnabled`. Quest is a single
    /// Talk step targeted at the recipient — the festival event surfaces dialogue with
    /// them naturally so the quest closes on Winter 25 without bespoke event hooks.
    private static QuestPosting? SecretGiftHint(QuestContext ctx)
    {
        if (!ModEntry.Config.SecretGiftHintEnabled)
            return null;

        NPC? recipient;
        try
        {
            recipient = StardewValley.Utility.GetRandomWinterStarParticipant();
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"SecretGiftHint: could not resolve Winter Star recipient: {ex.Message}", LogLevel.Trace);
            return null;
        }
        if (recipient == null)
            return null;

        var lovedNames = ResolveLovedItemNames(ctx, recipient.Name, max: 3);
        string lovedList = lovedNames.Count > 0
            ? string.Join(", ", lovedNames)
            : ModEntry.I18n.Get("quest.festival.secretGiftHint.fallback");

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverWinterStarGift",
                Kind = AdventureStepKind.Talk,
                Targets = new List<string> { recipient.Name },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.festival.secretGiftHint.step", new { recipient = recipient.displayName, items = lovedList })
            }
        }, giver: "Lewis", completionDialogue: ModEntry.I18n.Get("quest.festival.secretGiftHint.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Special,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Lewis",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Title = ModEntry.I18n.Get("quest.festival.secretGiftHint.title"),
            Description = ModEntry.I18n.Get("quest.festival.secretGiftHint.description", new { recipient = recipient.displayName, items = lovedList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.secretGiftHint.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// CSV row 73. Winter 20 DateLocked, mod-gated on Si.ExtraCraftingMaterials (Nexus
    /// 25467). Lewis asks the player to ship Paper + Tape so the town can wrap their
    /// Winter Star gifts. Reward = a Book of Stars from the same mod. Item ids are
    /// configurable so the quest still works if the source mod renames items.
    private static QuestPosting? WrappingPaper(QuestContext ctx)
    {
        if (!ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.SiExtraCraftingMaterials))
            return null;

        string paperId = string.IsNullOrWhiteSpace(ModEntry.Config.WrappingPaperPaperId)
            ? "Si.ECM_Paper"
            : ModEntry.Config.WrappingPaperPaperId;
        string tapeId = string.IsNullOrWhiteSpace(ModEntry.Config.WrappingPaperTapeId)
            ? "Si.ECM_Tape"
            : ModEntry.Config.WrappingPaperTapeId;
        string bookId = string.IsNullOrWhiteSpace(ModEntry.Config.WrappingPaperBookOfStarsId)
            ? "Si.ECM_BookOfStars"
            : ModEntry.Config.WrappingPaperBookOfStarsId;

        var paper = ctx.Items.TryResolveItem(paperId);
        var tape = ctx.Items.TryResolveItem(tapeId);
        if (paper == null || tape == null)
        {
            ctx.Monitor.Log($"WrappingPaper: Paper ({paperId}) or Tape ({tapeId}) item not found in registry; skipping. Override item ids in More Quests config if the source mod renamed them.", LogLevel.Trace);
            return null;
        }

        const string giver = "Lewis";
        const int qtyPerItem = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPaper",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { paper.QualifiedItemId },
                Count = qtyPerItem,
                Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.step.paper", new { count = qtyPerItem, item = paper.DisplayName })
            },
            new AdventureStepState
            {
                Name = "ShipTape",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { tape.QualifiedItemId },
                Count = qtyPerItem,
                Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.step.tape", new { count = qtyPerItem, item = tape.DisplayName })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new ObjectReward(bookId) },
            Title = ModEntry.I18n.Get("quest.festival.wrappingPaper.title"),
            Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.description", new { paper = paper.DisplayName, tape = tape.DisplayName, count = qtyPerItem }),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 9.5a helpers --------------------

    /// Picks a single random loved or liked item id from the NPC's `Data/NPCGiftTastes`
    /// entry, then resolves it through `ItemResolver`. Skips items that can't be
    /// resolved (modded id whose source mod isn't loaded, etc.) and falls back through
    /// the candidate pool until one resolves.
    private static ResolvedItem? PickLovedOrLikedItem(QuestContext ctx, string npc)
    {
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var tasteData))
            return null;
        var fields = tasteData.Split('/');
        if (fields.Length < 4)
            return null;

        var candidates = new List<string>();
        AppendIds(candidates, fields[1]); // loved
        AppendIds(candidates, fields[3]); // liked
        if (candidates.Count == 0)
            return null;

        // Try a handful of candidates so a stale or modded-only id doesn't poison the pick.
        for (int i = 0; i < 10 && candidates.Count > 0; i++)
        {
            int idx = Game1.random.Next(candidates.Count);
            string id = candidates[idx];
            candidates.RemoveAt(idx);
            // Skip negative category tokens — those are "any item in category N" and
            // can't be resolved to a single item directly.
            if (int.TryParse(id, out int n) && n < 0)
                continue;
            var resolved = ctx.Items.TryResolveItem(id);
            if (resolved != null)
                return resolved;
        }
        return null;
    }

    private static void AppendIds(List<string> sink, string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            sink.Add(part);
    }

    /// Returns up to `max` display names of items the NPC loves (with liked items
    /// appended if loved doesn't fill the cap). Used by the Secret Gift Hint
    /// description so the journal entry actually carries the hint.
    private static List<string> ResolveLovedItemNames(QuestContext ctx, string npc, int max)
    {
        var names = new List<string>(max);
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var tasteData))
            return names;
        var fields = tasteData.Split('/');
        if (fields.Length < 4)
            return names;

        AppendResolvedNames(ctx, fields[1], names, max);
        if (names.Count < max)
            AppendResolvedNames(ctx, fields[3], names, max);
        return names;
    }

    private static void AppendResolvedNames(QuestContext ctx, string raw, List<string> sink, int max)
    {
        if (string.IsNullOrEmpty(raw))
            return;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sink.Count >= max)
                return;
            if (int.TryParse(part, out int n) && n < 0)
                continue;
            var resolved = ctx.Items.TryResolveItem(part);
            if (resolved == null)
                continue;
            if (sink.Contains(resolved.DisplayName, StringComparer.Ordinal))
                continue;
            sink.Add(resolved.DisplayName);
        }
    }

    /// Cold-food vanilla pool. Plain melons (vanilla farm) plus Ice Cream (Traveling
    /// Cart / shop-bought) and Triple Shot Espresso (cold drink). Modded cold items
    /// get folded in below via the context-tag scan, so a content pack adding a cold
    /// drink that flags itself with `cold_drink_item` lands in the pool automatically.
    private static readonly string[] ColdContextTags =
    {
        "cold_drink_item",
        "ice_cream_item"
    };

    private static readonly string[] VanillaColdItemIds =
    {
        "(O)254", // Melon
        "(O)233", // Ice Cream
        "(O)253"  // Triple Shot Espresso
    };

    private static List<ResolvedItem> ResolveColdTaggedItems(QuestContext ctx)
    {
        var results = new List<ResolvedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in VanillaColdItemIds)
        {
            var resolved = ctx.Items.TryResolveItem(id);
            if (resolved != null && seen.Add(resolved.QualifiedItemId))
                results.Add(resolved);
        }

        // Modded cold-drink / ice-cream items via context tag scan.
        try
        {
            foreach (var itemType in StardewValley.ItemRegistry.ItemTypes)
            {
                if (itemType.Identifier != "(O)")
                    continue;
                foreach (var bareId in itemType.GetAllIds())
                {
                    string qualified = itemType.Identifier + bareId;
                    if (seen.Contains(qualified))
                        continue;
                    var data = StardewValley.ItemRegistry.GetData(qualified);
                    if (data?.RawData is not StardewValley.GameData.Objects.ObjectData obj)
                        continue;
                    if (obj.ContextTags == null)
                        continue;
                    bool matches = false;
                    foreach (var tag in obj.ContextTags)
                    {
                        for (int i = 0; i < ColdContextTags.Length; i++)
                        {
                            if (string.Equals(tag, ColdContextTags[i], StringComparison.OrdinalIgnoreCase))
                            {
                                matches = true;
                                break;
                            }
                        }
                        if (matches)
                            break;
                    }
                    if (!matches)
                        continue;
                    var resolved = ctx.Items.TryResolveItem(qualified);
                    if (resolved != null && seen.Add(resolved.QualifiedItemId))
                        results.Add(resolved);
                }
            }
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"ResolveColdTaggedItems: {ex.Message}", LogLevel.Warn);
        }

        return results;
    }

    /// Walks `Data/Crops` for seeds whose Seasons list excludes Winter. Returns the
    /// resolved seed items so the picker can hand one to the Ship objective.
    private static List<ResolvedItem> ResolveNonWinterSeeds(QuestContext ctx)
    {
        var results = new List<ResolvedItem>();
        try
        {
            foreach (var (seedId, data) in ctx.Data.Crops)
            {
                if (data.Seasons == null || data.Seasons.Count == 0)
                    continue;
                bool hasWinter = false;
                foreach (var s in data.Seasons)
                {
                    if (string.Equals(s.ToString(), "winter", StringComparison.OrdinalIgnoreCase))
                    {
                        hasWinter = true;
                        break;
                    }
                }
                if (hasWinter)
                    continue;
                var seed = ctx.Items.TryResolveItem("(O)" + seedId);
                if (seed != null)
                    results.Add(seed);
            }
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"ResolveNonWinterSeeds: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    /// Vanilla rare forage merged with anything carrying the `forage_item` context tag
    /// AND not an obviously common pick (drops anything tagged `season_<current>` so the
    /// daily-board posting feels rare, not an expanded SeasonalForaging).
    private static List<ResolvedItem> ResolveRareForage(QuestContext ctx)
    {
        var results = new List<ResolvedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, _) in RareForagePool)
        {
            var resolved = ctx.Items.TryResolveItem(id);
            if (resolved != null && seen.Add(resolved.QualifiedItemId))
                results.Add(resolved);
        }

        // Append modded forage that isn't tagged with the current season — current-season
        // forage is the BasicForaging quest's pool; the rare hunt should feel like an
        // actual hunt.
        string currentSeasonTag = "season_" + ctx.Season.ToLowerInvariant();
        var allForage = ctx.Items.GetForageItems();
        foreach (var item in allForage)
        {
            if (seen.Contains(item.QualifiedItemId))
                continue;
            bool inSeason = false;
            foreach (var tag in item.ContextTags)
            {
                if (string.Equals(tag, currentSeasonTag, StringComparison.OrdinalIgnoreCase))
                {
                    inSeason = true;
                    break;
                }
            }
            if (inSeason)
                continue;
            seen.Add(item.QualifiedItemId);
            results.Add(item);
        }
        return results;
    }

    /// Picks a current-season seed via Data/Crops + the existing seed-resolver pattern
    /// from PierresStockUp. Returns null if no seasonal crops resolve (e.g. on saves
    /// where every seasonal crop's seed got removed by a content pack).
    private static ResolvedItem? PickSeasonalSeed(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;
        // Try a handful of crops so a missing seed entry doesn't kill the reward.
        var pool = new List<ResolvedItem>(crops);
        for (int i = 0; i < 10 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            var crop = pool[idx];
            pool.RemoveAt(idx);
            string? seedId = ResolveSeedIdForHarvest(ctx, crop.QualifiedItemId);
            if (string.IsNullOrEmpty(seedId))
                continue;
            var seed = ctx.Items.TryResolveItem(seedId!);
            if (seed != null)
                return seed;
        }
        return null;
    }

    private static ResolvedItem? PickResolved(QuestContext ctx, (string Id, string Name)[] pool)
    {
        if (pool.Length == 0)
            return null;
        // Try a few to skip missing modded ids.
        var indices = new List<int>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
            indices.Add(i);
        while (indices.Count > 0)
        {
            int j = Game1.random.Next(indices.Count);
            var resolved = ctx.Items.TryResolveItem(pool[indices[j]].Id);
            indices.RemoveAt(j);
            if (resolved != null)
                return resolved;
        }
        return null;
    }

    // -------------------- Phase 9.5b: Quality-aware delivery quests --------------------

    /// CSV row 58. Daily-board ItemDelivery for Gold-quality (Quality=2) seasonal crops.
    /// Picks any met NPC as the requester. Reward scales with the crop's sell price
    /// between `GoldBasicBase` and `GoldIntermediateBase` plus `FriendshipBasic` to the
    /// requester. Skill-gated to Farming 4 via the JSON `SkillLevel` filter so the quest
    /// only surfaces once the player can plausibly produce gold-quality crops (gold
    /// requires the Tiller profession or a high farming level + fertilizer).
    private static QuestPosting? QualityCropDelivery(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;
        var crop = crops[Game1.random.Next(crops.Count)];

        int qty = Game1.random.Next(3, 7);
        int basePrice = Math.Max(crop.SellPrice, 30);
        int gold = Math.Clamp(
            (int)(basePrice * qty * ctx.Config.RewardMultiplierAboveSell),
            ctx.Config.GoldBasicBase,
            ctx.Config.GoldIntermediateBase);

        string qualityName = QualityName(2);

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            MinQuality = 2,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.farming.qualityCrop.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.farming.qualityCrop.description", new { npc = giver, qty, quality = qualityName, item = crop.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.farming.qualityCrop.objective", new { qty, quality = qualityName, item = crop.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.qualityCrop.targetMessage")
        };
    }

    /// CSV row 56. Daily-board ItemDelivery for Iridium-quality (Quality=4) "rare"
    /// seasonal crops, filtered to a sell-price band so the request feels premium.
    /// Picks any met NPC as the requester. Reward = `GoldAdvancedBase` plus a small
    /// stack of one rare/ancient seed. Skill-gated to Farming 7.
    private static QuestPosting? PremiumCropOrder(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        // Filter to "rare" tier by sell price. Vanilla high-end crops (Starfruit 750,
        // Ancient Fruit 550, Sweet Gem Berry 3000, Cranberries 75 base...) — the 200g
        // floor catches all the high-end seasonal crops without including run-of-the-mill
        // staples like Tomato (60) or Pumpkin (320 — yes, included, intentionally).
        var rare = crops.Where(c => c.SellPrice >= 200).ToList();
        if (rare.Count == 0)
            return null;
        var crop = rare[Game1.random.Next(rare.Count)];

        int qty = Game1.random.Next(2, 5);
        int gold = ctx.Config.GoldAdvancedBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var seedReward = PickRareSeed(ctx);
        if (seedReward != null)
            rewards.Add(new ObjectReward(seedReward.QualifiedItemId, 3));

        string qualityName = QualityName(4);

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            MinQuality = 4,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.farming.premiumCrop.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.farming.premiumCrop.description", new { npc = giver, qty, quality = qualityName, item = crop.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.farming.premiumCrop.objective", new { qty, quality = qualityName, item = crop.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.premiumCrop.targetMessage")
        };
    }

    /// CSV row 59. Daily-board ItemDelivery for Gold-quality (Quality=2) fish to Willy.
    /// Implemented as `ItemDelivery` rather than `Fishing` because the player needs to
    /// hold the gold-quality stack at turn-in time — `MoreQuestsFishingQuest`'s catch
    /// counter ticks on every catch regardless of quality, which would falsely show
    /// "5/5 caught" even when the player only had silver fish to deliver. The CSV row
    /// note explicitly accepts either approach; ItemDelivery is the simpler match for
    /// quality enforcement.
    private static QuestPosting? QualityFishDelivery(QuestContext ctx)
    {
        const string giver = "Willy";

        var fish = ctx.Config.FishingIgnoresVisitedLocations
            ? ctx.Items.GetSeasonalFish(ctx.Season)
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season);
        if (fish.Count == 0)
            return null;
        var target = fish[Game1.random.Next(fish.Count)];

        int qty = Game1.random.Next(3, 6);
        int basePrice = Math.Max(target.SellPrice, 30);
        int gold = Math.Clamp(
            (int)(basePrice * qty * ctx.Config.RewardMultiplierAboveSell),
            ctx.Config.GoldBasicBase,
            ctx.Config.GoldIntermediateBase);

        string qualityName = QualityName(2);

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            MinQuality = 2,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.fishing.qualityFish.title"),
            Description = ModEntry.I18n.Get("quest.fishing.qualityFish.description", new { qty, quality = qualityName, item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.qualityFish.objective", new { qty, quality = qualityName, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.qualityFish.targetMessage")
        };
    }

    /// Vanilla rare/ancient seeds for the Premium Crop Order reward. Resolved at
    /// pick-time so a missing modded id falls through to the next entry.
    private static readonly (string Id, string Name)[] RareSeedPool =
    {
        ("(O)499", "Ancient Seeds"),
        ("(O)347", "Rare Seed")
    };

    private static ResolvedItem? PickRareSeed(QuestContext ctx) => PickResolved(ctx, RareSeedPool);

    /// Maps a vanilla quality value (0/1/2/4) to its translated display name. Quality 3
    /// is unused by vanilla; the `_` fallback keeps the helper safe if a future
    /// definition somehow ships with that value.
    private static string QualityName(int quality) => quality switch
    {
        1 => ModEntry.I18n.Get("quest.quality.silver"),
        2 => ModEntry.I18n.Get("quest.quality.gold"),
        4 => ModEntry.I18n.Get("quest.quality.iridium"),
        _ => ModEntry.I18n.Get("quest.quality.normal")
    };

    // -------------------- Phase 9.5d: Festival decor-supply quests --------------------

    /// Curated decor pool for the Dance of the Moonlight Jellies festival reward.
    /// Vanilla Big-Craftable / Furniture ids picked for thematic fit (lights, decor).
    /// Unknown ids silently no-op via `RewardApplier`, so over-listing is safe across
    /// game versions.
    private static readonly string[] MoonlightJelliesDecorPool = { "(BC)21", "(BC)74", "(BC)272" };
    private static readonly string[] EggFestivalDecorPool = { "(BC)272", "(BC)143", "(BC)74" };
    private static readonly string[] LuauDecorPool = { "(BC)73", "(BC)74", "(BC)272" };

    /// Picks one item id from a curated decor pool. Returns the id verbatim — any
    /// resolution / instantiation happens later in `RewardApplier.ApplyOne` via
    /// `ItemRegistry.Create`. Empty pools return null (caller skips the reward step).
    private static string? PickDecor(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return null;
        return pool[Game1.random.Next(pool.Length)];
    }

    /// Splits a comma-separated NPC list (from `ModConfig.EastScarpFestivalNpcs` /
    /// `RidgesideFestivalNpcs`) into a deduplicated list of trimmed names. Empty entries
    /// are dropped; case-insensitive dedup so "Rosa, rosa" only counts once.
    private static List<string> SplitNpcList(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
                result.Add(trimmed);
        }
        return result;
    }

    /// Row 20 — Dance of the Moonlight Jellies Festival Decor Supply (Lewis, Summer 24).
    /// Two-step Ship Adventure: Torches + Wood. Reward = GoldBasicBase + one random decor
    /// item from a curated Moonlight-Jellies pool. Decor-shipping bypass enabled in case
    /// any of the modded extension items in the pool wouldn't normally ship.
    private static QuestPosting? MoonlightJelliesFestivalDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        const string giver = "Lewis";
        const int torchCount = 5;
        const int woodCount = 10;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipTorches",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)93" },
                Count = torchCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.step.torches", new { count = torchCount })
            },
            new AdventureStepState
            {
                Name = "ShipWood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)388" },
                Count = woodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.step.wood", new { count = woodCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldBasicBase) };
        var decor = PickDecor(MoonlightJelliesDecorPool);
        if (!string.IsNullOrEmpty(decor))
            rewards.Add(new ObjectReward(decor));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.moonlightJellies.title"),
            Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.description", new { torches = torchCount, wood = woodCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 22 — Egg Festival Decor Supply (Lewis, Spring 10). Single-step Ship Adventure
    /// for Hay Bales (a Big-Craftable that vanilla won't ship without the bypass).
    /// Reward = GoldBeginnerBase + one random decor from a curated Egg-Festival pool.
    private static QuestPosting? EggFestivalDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        const string giver = "Lewis";
        const int hayBaleCount = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipHayBales",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)45" },
                Count = hayBaleCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.step.hayBales", new { count = hayBaleCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldBeginnerBase) };
        var decor = PickDecor(EggFestivalDecorPool);
        if (!string.IsNullOrEmpty(decor))
            rewards.Add(new ObjectReward(decor));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.description", new { count = hayBaleCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 23 — Stardew Valley Fair Decor Supply (Lewis, Fall 12). Three-step Ship
    /// Adventure: Wood + Wood Signs + flowers (any vanilla flower-category Object).
    /// Reward = `FestivalBiasReward(Fair, FestivalBiasFairMagnitude)` so the Fair-day
    /// grange judging bumps in the player's favour. Wood Signs need the decor bypass.
    private static QuestPosting? FairFestivalDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        const string giver = "Lewis";
        const int woodCount = 10;
        const int signCount = 3;
        const int flowerCount = 5;
        const int flowerCategory = -80;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipWood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)388" },
                Count = woodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.wood", new { count = woodCount })
            },
            new AdventureStepState
            {
                Name = "ShipSigns",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)37" },
                Count = signCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.signs", new { count = signCount })
            },
            new AdventureStepState
            {
                Name = "ShipFlowers",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { $"$category:{flowerCategory}" },
                Count = flowerCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.flowers", new { count = flowerCount })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards =
            {
                new FestivalBiasReward(FestivalKind.Fair, Math.Max(1, ModEntry.Config.FestivalBiasFairMagnitude))
            },
            Title = ModEntry.I18n.Get("quest.festival.fairDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.fairDecor.description", new { wood = woodCount, signs = signCount, flowers = flowerCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 24 — Luau Decor Supply (Lewis, Summer 6). Three-step Ship Adventure: Fiber +
    /// Log Section ("Basic Log" furniture) + Wood Lamp-post. Reward = GoldIntermediateBase
    /// + one random decor from a curated Luau pool. Both furniture / Big-Craftable steps
    /// need the decor-shipping bypass.
    private static QuestPosting? LuauFestivalDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        const string giver = "Lewis";
        const int fiberCount = 10;
        const int logCount = 1;
        const int lampCount = 1;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipFiber",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)771" },
                Count = fiberCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.fiber", new { count = fiberCount })
            },
            new AdventureStepState
            {
                Name = "ShipBasicLog",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(F)1376" },
                Count = logCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.basicLog", new { count = logCount })
            },
            new AdventureStepState
            {
                Name = "ShipLampPost",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)21" },
                Count = lampCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.lampPost", new { count = lampCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldIntermediateBase) };
        var decor = PickDecor(LuauDecorPool);
        if (!string.IsNullOrEmpty(decor))
            rewards.Add(new ObjectReward(decor));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.luauDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.luauDecor.description", new { fiber = fiberCount, log = logCount, lamp = lampCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 26 — Spirit's Eve Decor Supply (Lewis, Fall 22). Three-step Ship Adventure:
    /// Pumpkins + Cloth + Torches. Reward = GoldIntermediateBase + Jack o' Lantern.
    /// Decor bypass enabled mostly for parity; vanilla ships all three objective items
    /// without help.
    private static QuestPosting? SpiritsEveDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        const string giver = "Lewis";
        const int pumpkinCount = 5;
        const int clothCount = 3;
        const int torchCount = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPumpkins",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)276" },
                Count = pumpkinCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.pumpkins", new { count = pumpkinCount })
            },
            new AdventureStepState
            {
                Name = "ShipCloth",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)428" },
                Count = clothCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.cloth", new { count = clothCount })
            },
            new AdventureStepState
            {
                Name = "ShipTorches",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)93" },
                Count = torchCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.torches", new { count = torchCount })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards =
            {
                new MoneyReward(ctx.Config.GoldIntermediateBase),
                new ObjectReward("(BC)126")
            },
            Title = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.description", new { pumpkins = pumpkinCount, cloth = clothCount, torches = torchCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 21 — East Scarp Spirit's Eve Decor Supply (Rosa, Fall 24). Mod-gated on the
    /// East Scarp / Eli & Dylan / Lurking in the Dark modset (any of the three lights up
    /// the role). Three-step Ship Adventure: purple-dye items + slime + stone. Reward =
    /// `FriendshipMultiHeart` to each named ESV festival NPC; the reward summary collapses
    /// 3+ named friendships into one generic line so the loved-by pool isn't spoiled.
    private static QuestPosting? EastScarpSpiritsEveDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        if (!MoreQuestsFramework.ModCompat.HasEs(ctx.Helper.ModRegistry))
            return null;
        const string giver = "Rosa";
        const int purpleCount = 5;
        const int slimeCount = 10;
        const int stoneCount = 20;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPurpleDye",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "$tag:color_purple" },
                Count = purpleCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.step.purple", new { count = purpleCount })
            },
            new AdventureStepState
            {
                Name = "ShipSlime",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)766" },
                Count = slimeCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.step.slime", new { count = slimeCount })
            },
            new AdventureStepState
            {
                Name = "ShipStone",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)390" },
                Count = stoneCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.step.stone", new { count = stoneCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>();
        foreach (var npc in SplitNpcList(ModEntry.Config.EastScarpFestivalNpcs))
            rewards.Add(new FriendshipReward(npc, ctx.Config.FriendshipMultiHeart));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.title"),
            Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.description", new { purple = purpleCount, slime = slimeCount, stone = stoneCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 25 — Ridgeside Gathering Decor Supply (Lenny, Fall 15). Mod-gated on Ridgeside
    /// Village. Three-step Ship Adventure: Tub o' Flowers + Wood + any table furniture
    /// (matched via `$tag:furniture_table` so vanilla AND modded tables count). Reward =
    /// `FriendshipMultiHeart` to each named RSV festival NPC. The Tub o' Flowers crafting
    /// recipe is granted at quest-accept by `MoreQuests.ModEntry.OnQuestAccepted` if the
    /// player doesn't already know it (so they can craft tubs without scrambling for the
    /// vanilla recipe).
    private static QuestPosting? RidgesideGatheringDecor(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        if (!MoreQuestsFramework.ModCompat.HasRsv(ctx.Helper.ModRegistry))
            return null;
        const string giver = "Lenny";
        const int tubCount = 1;
        const int woodCount = 20;
        const int tableCount = 1;

        string tubId = string.IsNullOrWhiteSpace(ModEntry.Config.RsvTubOFlowersId)
            ? "(BC)272"
            : ModEntry.Config.RsvTubOFlowersId;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipTubOFlowers",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { tubId },
                Count = tubCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.tub", new { count = tubCount })
            },
            new AdventureStepState
            {
                Name = "ShipWood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)388" },
                Count = woodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.wood", new { count = woodCount })
            },
            new AdventureStepState
            {
                Name = "ShipTables",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "$tag:furniture_table" },
                Count = tableCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.table", new { count = tableCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>();
        foreach (var npc in SplitNpcList(ModEntry.Config.RidgesideFestivalNpcs))
            rewards.Add(new FriendshipReward(npc, ctx.Config.FriendshipMultiHeart));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.rsvGathering.title"),
            Description = ModEntry.I18n.Get("quest.festival.rsvGathering.description", new { wood = woodCount }),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 9.5e: Fishing-track quests --------------------

    /// Curated rare-tackle pool for the Rainy Day Catch reward. All vanilla qualified ids;
    /// modded saves get the same pool unless the picker resolves to a missing id, in which
    /// case the pick falls through.
    private static readonly (string Id, string Name)[] RainyDayTacklePool =
    {
        ("(O)694", "Spinner"),
        ("(O)695", "Trap Bobber"),
        ("(O)691", "Barbed Hook"),
        ("(O)693", "Treasure Hunter"),
        ("(O)877", "Curiosity Lure")
    };

    /// Vanilla Challenge Bait — high-attract bait used as a 10-pack reward for the
    /// location overpopulation quest. Modded saves keep the same id; if a content pack
    /// removes the item the resolver returns null and the reward gracefully no-ops.
    private const string ChallengeBaitId = "(O)910";
    private const string BaitId = "(O)685";

    /// Row 13 — `<Location>` fish overpopulation. Daily-board FishingQuest gated on a
    /// specific location: pick a fish from the player's visited-location pool, then read
    /// its first eligible spawn location from the visited set so the quest description
    /// can ground the request in a real spot. Reward = `GoldIntermediateBase` + 10x
    /// Challenge Bait. Giver is dispatched via `EcologyMinded`.
    private static QuestPosting? LocationFishOverpopulation(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        // The CSV's "fish for a specific fish at a specific spot" only makes sense if the
        // player has actually visited a spawn location for the fish. Use the visited-only
        // helper so quests target reachable water; the helper falls back to the full pool
        // only if the visited set is empty (fresh save).
        var fish = ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season);
        if (fish.Count == 0)
            return null;

        // Try a handful of fish so a fish whose only spawn locations the player hasn't
        // actually visited drops out. Each pick walks Data/Locations to verify the fish
        // has at least one spawn in a visited spot.
        ResolvedItem? target = null;
        string? targetLocation = null;
        var pool = new List<ResolvedItem>(fish);
        for (int i = 0; i < 8 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            var candidate = pool[idx];
            pool.RemoveAt(idx);
            string? loc = ResolveVisitedSpawnLocation(ctx, candidate.QualifiedItemId);
            if (loc != null)
            {
                target = candidate;
                targetLocation = loc;
                break;
            }
        }
        if (target == null || targetLocation == null)
            return null;

        int qty = Math.Max(2, Math.Min(5, 2 + Game1.player.FishingLevel / 3));
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var bait = ctx.Items.TryResolveItem(ChallengeBaitId);
        if (bait != null)
            rewards.Add(new ObjectReward(ChallengeBaitId, 10));

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            CatchLocationName = targetLocation,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.locationOverpop.title"),
            Description = ModEntry.I18n.Get("quest.fishing.locationOverpop.description", new
            {
                npc = giver,
                qty,
                item = target.DisplayName,
                location = LocationDisplayName(targetLocation)
            }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.locationOverpop.objective", new
            {
                qty,
                item = target.DisplayName,
                location = LocationDisplayName(targetLocation)
            }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.locationOverpop.targetMessage")
        };
    }

    /// Row 61 — Rainy Day Catch. Daily-board FishingQuest filtered to fish whose
    /// `Data/Fish` weather field includes "rainy", with a runtime gate that the player is
    /// fishing in actual rain (Rain or Storm). Giver dispatched via `RainyDayFishing`.
    /// Reward = `GoldIntermediateBase` + one rare tackle.
    private static QuestPosting? RainyDayCatch(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.RainyDayFishing);
        if (giver == null)
            return null;

        // GetSeasonalFish accepts a weather filter that intersects against `Data/Fish`'s
        // weather field. "rainy" = vanilla token used for rain-only fish (Eel, Catfish,
        // Lingcod, etc.).
        var fish = ctx.Config.FishingIgnoresVisitedLocations
            ? ctx.Items.GetSeasonalFish(ctx.Season, "rainy")
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season, "rainy");
        if (fish.Count == 0)
            return null;
        var target = fish[Game1.random.Next(fish.Count)];

        int qty = Game1.random.Next(1, 4);
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var tackle = PickResolved(ctx, RainyDayTacklePool);
        if (tackle != null)
            rewards.Add(new ObjectReward(tackle.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            CatchWeather = "Rain",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.rainyDay.title"),
            Description = ModEntry.I18n.Get("quest.fishing.rainyDay.description", new { npc = giver, qty, item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.rainyDay.objective", new { qty, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.rainyDay.targetMessage")
        };
    }

    /// Row 68 — Small/Medium/Large fish overpopulation. Daily-board FishingQuest filtered
    /// by a size threshold (the catch's reported size in inches must clear the bucket
    /// floor). Bucket is sampled per-quest; description embeds the bucket label and one
    /// representative fish for flavour. Giver = Demetrius. Reward = `GoldIntermediateBase`
    /// + bait.
    private static QuestPosting? SizeFishOverpopulation(QuestContext ctx)
    {
        const string giver = "Demetrius";

        // Pick a bucket. Mapping bucket → minimum-size threshold (inches). Floor of the
        // bucket is what the runtime gate compares against `OnFishCaught(size)`.
        int bucket = Game1.random.Next(3); // 0=Small, 1=Medium, 2=Large
        int smallMax = Math.Max(1, ModEntry.Config.SizeBucketSmallMaxInches);
        int mediumMax = Math.Max(smallMax + 1, ModEntry.Config.SizeBucketMediumMaxInches);
        int minSize = bucket switch
        {
            0 => 1,
            1 => smallMax + 1,
            _ => mediumMax + 1
        };
        string bucketKey = bucket switch
        {
            0 => "small",
            1 => "medium",
            _ => "large"
        };

        // Pick a representative fish for the description — any seasonal fish; we don't
        // strictly require its max size to match the bucket because the quest accepts any
        // catch above the threshold. The ObjectiveItemId is set to this fish for vanilla
        // FishingQuest's counter, but the size gate is what actually bounds completion.
        var fish = ctx.Config.FishingIgnoresVisitedLocations
            ? ctx.Items.GetSeasonalFish(ctx.Season)
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season);
        if (fish.Count == 0)
            return null;
        var target = fish[Game1.random.Next(fish.Count)];

        int qty = Math.Max(2, Math.Min(5, 2 + Game1.player.FishingLevel / 3));
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var bait = ctx.Items.TryResolveItem(BaitId);
        if (bait != null)
            rewards.Add(new ObjectReward(BaitId, 25));

        string bucketLabel = ModEntry.I18n.Get($"quest.fishing.sizeOverpop.bucket.{bucketKey}");

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            CatchMinSize = minSize,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.sizeOverpop.title", new { bucket = bucketLabel }),
            Description = ModEntry.I18n.Get("quest.fishing.sizeOverpop.description", new
            {
                qty,
                item = target.DisplayName,
                bucket = bucketLabel,
                minSize
            }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.sizeOverpop.objective", new
            {
                qty,
                item = target.DisplayName,
                minSize
            }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.sizeOverpop.targetMessage")
        };
    }

    /// Row 60 — Rainbow Platter (Trout Derby, Summer 20-21). DateLocked yearly DailyBoard
    /// posting on Summer 20: catch `FestivalFishQty` Rainbow Trout (O)138. Giver dispatched
    /// via `SaloonChef`; reward = recipe (per-giver) + `ShopDiscountReward` on the dish for
    /// vanilla Gus saves only (the framework's discount writer needs a known shop id).
    private static QuestPosting? RainbowPlatter(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        const string rainbowTroutId = "(O)138";
        int qty = Math.Max(1, ModEntry.Config.FestivalFishQty);

        string recipeName = ResolveTroutDerbyRecipe(giver);
        var rewards = new List<RewardSpec>
        {
            new RecipeReward(recipeName)
        };
        // Only Gus has a known vanilla shop; for modded givers we grant just the recipe.
        if (string.Equals(giver, "Gus", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ModEntry.Config.TroutDerbyDishGus))
        {
            rewards.Add(new ShopDiscountReward(
                ShopId: "Saloon",
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays,
                AppliesTo: new List<string> { ModEntry.Config.TroutDerbyDishGus },
                GuaranteedStock: 5));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = rainbowTroutId,
            ObjectiveItemName = "Rainbow Trout",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.rainbowPlatter.title"),
            Description = ModEntry.I18n.Get("quest.festival.rainbowPlatter.description", new { npc = giver, qty, recipe = recipeName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.rainbowPlatter.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.rainbowPlatter.targetMessage")
        };
    }

    /// Row 70 — SquidFest Showcase (Winter 12-13). DateLocked yearly posting on Winter 12:
    /// catch `FestivalFishQty` Squid (O)151. Same shape as Rainbow Platter — saloon-chef
    /// giver, recipe reward per giver, ShopDiscountReward on the dish for vanilla Gus.
    private static QuestPosting? SquidFestShowcase(QuestContext ctx)
    {
        if (!ModEntry.Config.FestivalQuestsEnabled)
            return null;
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        const string squidId = "(O)151";
        int qty = Math.Max(1, ModEntry.Config.FestivalFishQty);

        string recipeName = ResolveSquidFestRecipe(giver);
        var rewards = new List<RewardSpec>
        {
            new RecipeReward(recipeName)
        };
        if (string.Equals(giver, "Gus", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ModEntry.Config.SquidFestDishGus))
        {
            rewards.Add(new ShopDiscountReward(
                ShopId: "Saloon",
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays,
                AppliesTo: new List<string> { ModEntry.Config.SquidFestDishGus },
                GuaranteedStock: 5));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = squidId,
            ObjectiveItemName = "Squid",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.squidFest.title"),
            Description = ModEntry.I18n.Get("quest.festival.squidFest.description", new { npc = giver, qty, recipe = recipeName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.squidFest.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.squidFest.targetMessage")
        };
    }

    private static string ResolveTroutDerbyRecipe(string giver) => giver switch
    {
        "Pika" => ModEntry.Config.TroutDerbyRecipePika,
        "Celestine" => ModEntry.Config.TroutDerbyRecipeCelestine,
        "Rosa" => ModEntry.Config.TroutDerbyRecipeRosa,
        _ => ModEntry.Config.TroutDerbyRecipeGus
    };

    private static string ResolveSquidFestRecipe(string giver) => giver switch
    {
        "Pika" => ModEntry.Config.SquidFestRecipePika,
        "Celestine" => ModEntry.Config.SquidFestRecipeCelestine,
        "Rosa" => ModEntry.Config.SquidFestRecipeRosa,
        _ => ModEntry.Config.SquidFestRecipeGus
    };

    /// Walks Data/Locations for the fish's spawn entries, intersected with the player's
    /// visited locations, returning the first matching location key. Returns null when
    /// the fish has no spawn in any visited spot. The CSV row asks for a fish at a
    /// specific spot, so we need an actual reachable location to ground the quest in.
    private static string? ResolveVisitedSpawnLocation(QuestContext ctx, string fishQualifiedId)
    {
        try
        {
            var visited = Game1.player?.locationsVisited;
            if (visited == null || visited.Count == 0)
                return null;
            var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);

            string season = ctx.Season;
            foreach (var (locName, data) in ctx.Data.Locations)
            {
                if (string.Equals(locName, "Default", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!visitedSet.Contains(locName))
                    continue;
                if (data.Fish == null)
                    continue;
                foreach (var spawn in data.Fish)
                {
                    if (spawn?.ItemId == null) continue;
                    if (spawn.Season.HasValue && !string.Equals(spawn.Season.Value.ToString(), season, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string qualified = StardewValley.ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    if (string.Equals(qualified, fishQualifiedId, StringComparison.OrdinalIgnoreCase))
                        return locName;
                }
            }
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"ResolveVisitedSpawnLocation: {ex.Message}", LogLevel.Warn);
        }
        return null;
    }

    /// Lightweight pretty-printer for vanilla location keys used in quest descriptions.
    /// Maps the common keys back to their in-game labels; unknown keys (modded) pass
    /// through verbatim, which is usually what the player sees on the map anyway.
    private static string LocationDisplayName(string key) => key?.ToLowerInvariant() switch
    {
        "town" => "Pelican Town",
        "beach" => "the beach",
        "mountain" => "the mountain lake",
        "forest" => "Cindersap Forest",
        "woods" => "the Secret Woods",
        "backwoods" => "the Backwoods",
        "desert" => "the Calico Desert",
        "submarine" => "the Night Market submarine",
        "islandsouth" or "islandnorth" or "islandwest" or "islandeast" or "islandsoutheast" => "Ginger Island",
        _ => key ?? string.Empty
    };
}
