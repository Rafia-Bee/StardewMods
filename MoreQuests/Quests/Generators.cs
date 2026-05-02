using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
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
}
