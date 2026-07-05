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
    // -------------------- Recipe-unlock mail quests --------------------

    /// One-time mail from a random adult villager the day after the player learns the Preserves
    /// Jar recipe. Asks for jams or pickles (OR-alternatives) to the shipping bin. Qty scales
    /// with Farming when scaling is on. No deadline (matches "Meet the Townsfolk"). Reward:
    /// FriendshipLarge to the giver.
    private static QuestPosting? PreservesJarRequest(QuestContext ctx)
    {
        return BuildFarmingShipRequest(
            ctx,
            primaryItemId: "(O)342",
            alternativeItemIds: new[] { "(O)344" },
            titleKey: "quest.farming.preservesJarRequest.title",
            descriptionKey: "quest.farming.preservesJarRequest.description",
            objectiveKey: "quest.farming.preservesJarRequest.objective",
            targetMessageKey: "quest.farming.preservesJarRequest.targetMessage");
    }

    /// One-time mail when the player learns the Keg recipe. Asks for any keg output: wine,
    /// juice, mead, beer, pale ale, coffee, or green tea. Same qty scaling as PreservesJarRequest.
    private static QuestPosting? KegRequest(QuestContext ctx)
    {
        return BuildFarmingShipRequest(
            ctx,
            primaryItemId: "(O)348",
            alternativeItemIds: new[] { "(O)350", "(O)459", "(O)346", "(O)303", "(O)395", "(O)614" },
            titleKey: "quest.farming.kegRequest.title",
            descriptionKey: "quest.farming.kegRequest.description",
            objectiveKey: "quest.farming.kegRequest.objective",
            targetMessageKey: "quest.farming.kegRequest.targetMessage");
    }

    /// One-time mail when the player learns the Dehydrator recipe. Asks for DriedMushrooms
    /// or DriedFruit (the parent id covers raisins and every other dried fruit variant).
    private static QuestPosting? DehydratorRequest(QuestContext ctx)
    {
        return BuildFarmingShipRequest(
            ctx,
            primaryItemId: "(O)DriedMushrooms",
            alternativeItemIds: new[] { "(O)DriedFruit" },
            titleKey: "quest.farming.dehydratorRequest.title",
            descriptionKey: "quest.farming.dehydratorRequest.description",
            objectiveKey: "quest.farming.dehydratorRequest.objective",
            targetMessageKey: "quest.farming.dehydratorRequest.targetMessage");
    }

    /// Shared scaffolding for Farming-scaled recipe-unlock Ship quests. Picks a random adult-human
    /// giver, sets OR-alternatives, rolls a Farming-scaled qty, builds with DeadlineDays = 0
    /// (un-timed). Preserves Jar / Keg / Dehydrator feed through here; Fish Smoker has its own.
    private static QuestPosting? BuildFarmingShipRequest(
        QuestContext ctx,
        string primaryItemId,
        IReadOnlyList<string> alternativeItemIds,
        string titleKey,
        string descriptionKey,
        string objectiveKey,
        string targetMessageKey)
    {
        var candidates = MetAdultHumanGiftReceivers();
        if (candidates.Count == 0)
            return null;
        string giver = candidates[Game1.random.Next(candidates.Count)];

        int qty = Difficulty.Scaled(ctx,
            () =>
            {
                int farming = Game1.player.FarmingLevel;
                int upper = Math.Max(5, farming * 3);
                return Game1.random.Next(5, upper + 1);
            },
            () => Game1.random.Next(2, 11));

        var posting = new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Ship,
            QuestGiver = giver,
            ObjectiveItemId = primaryItemId,
            ObjectiveItemName = string.Empty,
            ObjectiveQuantity = qty,
            ObjectiveItemWeight = 1,
            DeadlineDays = 0,
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipLarge) },
            Title = ModEntry.I18n.Get(titleKey),
            Description = ModEntry.I18n.Get(descriptionKey, new { qty }),
            CurrentObjective = ModEntry.I18n.Get(objectiveKey, new { qty }),
            TargetMessage = ModEntry.I18n.Get(targetMessageKey)
        };
        foreach (var alt in alternativeItemIds)
        {
            posting.AlternativeObjectiveItemIds.Add(alt);
            posting.AlternativeObjectiveItemWeights.Add(1);
        }
        return posting;
    }

    private static QuestPosting? BasicCropDelivery(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;

        var crop = crops[Game1.random.Next(crops.Count)];

        int qty = Difficulty.Scaled(ctx, QuestCategory.Farming,
            skill => skill + Game1.random.Next(2, 5),
            () => 10);

        int gold = (int)(crop.SellPrice * qty * ctx.Config.RewardMultiplierBelowSell);

        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Beginner,
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

    /// Bulk-crop delivery for Pierre. Picks RequestVariationCount distinct seasonal crops
    /// with Farming-scaled per-crop quantity. Reward: SeedShop discount scoped to the matching
    /// seed ids for SeedShopDiscountDurationDays at SeedShopDiscountPercent off.
    private static QuestPosting? PierresStockUp(QuestContext ctx)
    {
        int variationCount = Math.Clamp(ModEntry.Config.RequestVariationCount, 2, 5);

        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count < variationCount)
            return null;

        var pool = new List<ResolvedItem>(crops);
        var picks = new List<ResolvedItem>(variationCount);
        for (int i = 0; i < variationCount && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            picks.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        int qtyPer = Difficulty.Scaled(ctx,
            () => Math.Max(6, 4 + 2 * Game1.player.FarmingLevel),
            () => 12);

        const string giver = "Pierre";

        // Reverse-map each picked crop's harvest id to its seed id so the discount applies
        // specifically to the matching seeds. Misses just don't get discounted; the quest still posts.
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
                items = JoinItemList(picks.Select(p => p.DisplayName))
            }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.pierresStockUp.targetMessage"),
            PreBuiltQuest = quest
        };

        if (ModEntry.Config.SeedShopDiscountPercent > 0 && ModEntry.Config.SeedShopDiscountDurationDays > 0)
        {
            // A third of the requested haul (min 2) as the per-visit seed cap. Keeps the
            // discount window from becoming an unlimited farming press for ancient fruit etc.
            int guaranteedStock = Math.Max(2, qtyPer / 3);
            posting.Rewards.Add(new ShopDiscountReward(
                ShopId: "SeedShop",
                PercentOff: ModEntry.Config.SeedShopDiscountPercent,
                DurationDays: ModEntry.Config.SeedShopDiscountDurationDays,
                AppliesTo: seedIds.Count > 0 ? seedIds : null,
                GuaranteedStock: guaranteedStock));
        }

        return posting;
    }

    /// Morris (or SVE's MorrisTod) asks the farmer to ship qty of one seasonal crop. Reward
    /// is sell-price below market (Joja pays below sell so the headline figure looks high
    /// while lost crop value brings it back to break-even). Tier 1 consequence on NPCGiftTastes:
    /// villagers who love the crop praise the player; haters get a negative line + delta.
    private static QuestPosting? MassiveHarvestRequest(QuestContext ctx)
    {
        if (Game1.player.FarmingLevel < 7)
            return null; // Expert-tier, surface in the late-game band.

        // JojaCorpRep covers vanilla Morris + SVE's MorrisTod. Null on CC-route saves.
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

        var consequence = ctx.Config.ConsequencesEnabled
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

    /// ItemDelivery for Silver-or-better seasonal crops. Reward scales with sell price between
    /// GoldBasicBase and GoldIntermediateBase + FriendshipIntermediate. Farming 2 gate so the
    /// quest surfaces once silver crops show up reliably.
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

        int qty = Game1.random.Next(3, 11);
        int basePrice = Math.Max(crop.SellPrice, 30);
        int gold = Math.Clamp(
            (int)(basePrice * qty * ctx.Config.RewardMultiplierAboveSell),
            ctx.Config.GoldBasicBase,
            ctx.Config.GoldIntermediateBase);

        string qualityName = QualityName(1);

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            MinQuality = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipIntermediate)
            },
            Title = ModEntry.I18n.Get("quest.farming.qualityCrop.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.farming.qualityCrop.description", new { npc = giver, qty, quality = qualityName, item = crop.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.farming.qualityCrop.objective", new { qty, quality = qualityName, item = crop.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.qualityCrop.targetMessage")
        };
    }

    /// ItemDelivery for Iridium-quality seasonal crops, no sell-price filter. Qty scales with
    /// Farming when scaling is on. Reward: GoldAdvancedBase + rare/ancient seeds qty/3.
    /// Farming 7 gate.
    private static QuestPosting? PremiumCropOrder(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;
        var crop = crops[Game1.random.Next(crops.Count)];

        int qty = Difficulty.Scaled(ctx, QuestCategory.Farming,
            farmingLevel => farmingLevel * 3 + Game1.random.Next(1, 11),
            () => 10 + Game1.random.Next(1, 11));
        int gold = ctx.Config.GoldAdvancedBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var seedReward = PickRareSeed(ctx);
        if (seedReward != null)
            rewards.Add(new ObjectReward(seedReward.QualifiedItemId, qty / 3));

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

    /// Vanilla rare/ancient seeds for the Premium Crop Order reward.
    private static readonly (string Id, string Name)[] RareSeedPool =
    {
        ("(O)499", "Ancient Seeds"),
        ("(O)347", "Rare Seed")
    };

    private static ResolvedItem? PickRareSeed(QuestContext ctx) => PickResolved(ctx, RareSeedPool);

    /// Caroline asks for off-season edible forage or flowers she loves/likes (no herbs).
    /// Off-season: Y1 = seasons already passed, Y2+ = every season except the current.
    /// Qty scales with foraging level when scaling on, flat 5 when off. Reward: FriendshipMid + 2x Tea Leaves.
    private static QuestPosting? CarolineTeaGarden(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Caroline") == null)
            return null;

        var offSeasons = GetCarolineTeaOffSeasons(ctx.Year, ctx.Season);
        if (offSeasons.Count == 0)
            return null;

        if (!ctx.Data.GiftTastes.TryGetValue("Caroline", out var taste))
            return null;
        var fields = taste.Split('/');
        if (fields.Length < 4)
            return null;
        var loved = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var liked = fields[3].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        var pool = new List<ResolvedItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var season in offSeasons)
        {
            var forage = ctx.Config.ForagingIgnoresVisitedLocations
                ? ctx.Items.GetForageItems(season)
                : ctx.Items.GetForageItemsInVisitedLocations(season);
            foreach (var f in forage)
                if (seen.Add(f.QualifiedItemId))
                    pool.Add(f);
        }
        foreach (var f in ctx.Items.GetItemsByCategory(StardewValley.Object.flowersCategory))
        {
            if (!offSeasons.Any(s => f.ContextTags.Contains("season_" + s)))
                continue;
            if (seen.Add(f.QualifiedItemId))
                pool.Add(f);
        }

        pool = pool.Where(f =>
        {
            foreach (var t in f.ContextTags)
                if (t.IndexOf("herb", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            string bare = StripPrefix(f.QualifiedItemId);
            return loved.Contains(bare) || liked.Contains(bare);
        }).ToList();

        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];

        int qty = Difficulty.Scaled(ctx, QuestCategory.Foraging,
            foragingLevel => Math.Max(1, (int)(foragingLevel * 1.5)),
            () => 5);
        int teaCount = qty * 2;

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Caroline",
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Caroline", ctx.Config.FriendshipMid),
                new ObjectReward("(O)815", teaCount)
            },
            Title = ModEntry.I18n.Get("quest.farming.carolineTeaGarden.title"),
            Description = ModEntry.I18n.Get("quest.farming.carolineTeaGarden.description", new { qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.farming.carolineTeaGarden.objective", new { qty, item = pick.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.carolineTeaGarden.targetMessage")
        };
    }

    private static List<string> GetCarolineTeaOffSeasons(int year, string currentSeason)
    {
        var all = new[] { "spring", "summer", "fall", "winter" };
        string current = currentSeason?.ToLowerInvariant() ?? "";
        if (year >= 2)
            return all.Where(s => s != current).ToList();
        int idx = Array.IndexOf(all, current);
        if (idx <= 0)
            return new List<string>();
        return all.Take(idx).ToList();
    }

    /// "Grow a crop start to finish" multistep quest. A FarmerNPCs role giver asks the
    /// player to sow N seeds, water them, harvest the crop, and deliver the haul. 28d
    /// hardcoded deadline (longest-running daily-board quest in the framework). The
    /// crop is picked from the seasonal pool filtered by maturity-fits-in-window so
    /// late-season acceptances don't roll a crop that can't mature in time.
    /// Reward: gold = max(sellPrice, 20) * qty * RewardMultiplierBelowSell, plus
    /// Hyper Speed-Gro x (qty * 2).
    private static QuestPosting? CropCycleQuest(QuestContext ctx)
    {
        if (Game1.player.FarmingLevel < ModEntry.Config.CropCycleMinFarmingLevel)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.FarmerNPCs);
        if (giver == null)
            return null;

        // A full season. Doubles as the harvest window and the quest deadline (this is the
        // longest-running daily-board quest, so it runs right to the end of the season).
        const int seasonLength = 28;
        int daysLeftInSeason = seasonLength - Game1.dayOfMonth + 1;
        int harvestWindow = Math.Min(seasonLength, daysLeftInSeason);

        var viable = FilterCropsByGrowthWindow(ctx, harvestWindow);
        if (viable.Count == 0)
            return null;
        var crop = viable[Game1.random.Next(viable.Count)];

        string? seedId = ResolveSeedIdForHarvest(ctx, crop.QualifiedItemId);
        if (string.IsNullOrEmpty(seedId))
            return null;
        var seed = ctx.Items.TryResolveItem(seedId!);
        if (seed == null)
            return null;

        int qty = Difficulty.Scaled(ctx,
            () =>
            {
                int farming = Game1.player.FarmingLevel;
                int upper = Math.Max(4, farming * 2);
                return 5 + Game1.random.Next(3, upper + 1);
            },
            () => Game1.random.Next(1, 11));

        int basePrice = Math.Max(crop.SellPrice, 20);
        int gold = (int)(basePrice * qty * ctx.Config.RewardMultiplierBelowSell);
        int hyperSpeedGroCount = qty * 2;

        var steps = new List<AdventureStepState>
        {
            new()
            {
                Name = "Sow",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.CropCycleSowHandler },
                Items = new List<string> { seedId!, crop.QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.farming.cropCycle.step.sow", new { qty, item = crop.DisplayName })
            },
            new()
            {
                Name = "Water",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.CropCycleWaterHandler },
                Items = new List<string> { crop.QualifiedItemId, seedId! },
                Count = qty,
                Requires = new List<string> { "Sow" },
                Description = ModEntry.I18n.Get("quest.farming.cropCycle.step.water", new { qty, item = crop.DisplayName })
            },
            new()
            {
                Name = "Harvest",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.CropCycleHarvestHandler },
                Items = new List<string> { crop.QualifiedItemId },
                Count = qty,
                Requires = new List<string> { "Water" },
                Description = ModEntry.I18n.Get("quest.farming.cropCycle.step.harvest", new { qty, item = crop.DisplayName })
            },
            new()
            {
                Name = "Deliver",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { giver },
                Items = new List<string> { crop.QualifiedItemId },
                Count = qty,
                Requires = new List<string> { "Harvest" },
                Description = ModEntry.I18n.Get("quest.farming.cropCycle.step.deliver", new { qty, item = crop.DisplayName, npc = giver })
            }
        };

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.farming.cropCycle.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = seasonLength,
            Rewards =
            {
                new MoneyReward(gold),
                new ObjectReward("(O)918", hyperSpeedGroCount)
            },
            Title = ModEntry.I18n.Get("quest.farming.cropCycle.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.farming.cropCycle.description", new { npc = giver, qty, item = crop.DisplayName, seed = seed.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.cropCycle.targetMessage"),
            PreBuiltQuest = quest
        };
    }

}
