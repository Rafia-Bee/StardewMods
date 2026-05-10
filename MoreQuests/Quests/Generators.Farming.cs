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
    private static QuestPosting? BasicCropDelivery(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;

        var crop = crops[Game1.random.Next(crops.Count)];
        int skill = Difficulty.GetSkillLevel(QuestCategory.Farming);

        int qty = ctx.Config.DifficultyScaling
            ? skill + Game1.random.Next(2, 5)
            : 10;

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

    /// CSV row 54. Daily-board bulk-crop delivery for Pierre's General Store. Picks
    /// `RequestVariationCount` distinct seasonal crops; player delivers a per-crop
    /// quantity that scales with Farming skill. Reward = `ShopDiscount` on Pierre's
    /// `SeedShop` for the matching seed ids, lasting `SeedShopDiscountDurationDays`
    /// in-game days at `SeedShopDiscountPercent` off.
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
                items = JoinItemList(picks.Select(p => p.DisplayName))
            }),
            TargetMessage = ModEntry.I18n.Get("quest.farming.pierresStockUp.targetMessage"),
            PreBuiltQuest = quest
        };

        if (ModEntry.Config.SeedShopDiscountPercent > 0 && ModEntry.Config.SeedShopDiscountDurationDays > 0)
        {
            // A third of the requested haul as the per-visit seed cap on quest-injected
            // entries (minimum 2). Picking qtyPer/3 keeps the discount window from
            // becoming an unlimited farming press for high-value crops like ancient fruit.
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

    /// Walks `Data/Crops` for a row whose `HarvestItemId` matches the requested crop, and
    /// returns the qualified seed id (the dictionary key is the seed's bare item id). Used
    /// to scope Pierre's seed-shop discount to the seeds for the quested crops.

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

    /// CSV row 58. Daily-board ItemDelivery for Silver-or-better (Quality>=1) seasonal
    /// crops. Picks any met NPC as the requester. Reward scales with the crop's sell
    /// price between `GoldBasicBase` and `GoldIntermediateBase` plus
    /// `FriendshipIntermediate` to the requester. Skill-gated to Farming 2 so the quest
    /// surfaces once the player has a few seasons under their belt and silver crops
    /// start showing up reliably.
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

    /// CSV row 56. Daily-board ItemDelivery for Iridium-quality (Quality=4) seasonal
    /// crops. Iridium is the only gating constraint, no sell-price filter. Picks any
    /// met NPC as the requester. Quantity scales with Farming skill when difficulty
    /// scaling is on, otherwise rolls a flat band. Reward = `GoldAdvancedBase` plus
    /// a stack of rare/ancient seeds equal to qty/3. Skill-gated to Farming 7.
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

        int farmingLevel = Difficulty.GetSkillLevel(QuestCategory.Farming);
        int qty = ctx.Config.DifficultyScaling
            ? farmingLevel * 3 + Game1.random.Next(1, 11)
            : 10 + Game1.random.Next(1, 11);
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

    /// CSV row 59. Daily-board ItemDelivery for Gold-quality (Quality=2) fish to Willy.
    /// Implemented as `ItemDelivery` rather than `Fishing` because the player needs to
    /// hold the gold-quality stack at turn-in time — `MoreQuestsFishingQuest`'s catch
    /// counter ticks on every catch regardless of quality, which would falsely show
    /// "5/5 caught" even when the player only had silver fish to deliver. The CSV row
    /// note explicitly accepts either approach; ItemDelivery is the simpler match for
    /// quality enforcement.

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

    /// CSV row 15. Daily-board ItemDeliveryQuest. Caroline asks for off-season edible
    /// forage or flowers she loves or likes (no herbs) so she can brew a new batch of
    /// tea. Off-season pool: Y1 = seasons that have already passed in the current year
    /// (so Y1 spring skips), Y2+ = every season except the current one. Quantity scales
    /// with foraging level when DifficultyScaling is on; flat 5 when off. Reward =
    /// `FriendshipMid` to Caroline + Tea Leaves equal to twice the requested quantity.
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

        int foragingLevel = Difficulty.GetSkillLevel(QuestCategory.Foraging);
        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(1, (int)(foragingLevel * 1.5))
            : 5;
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

    /// CSV row 17. Daily-board single-step `ClearDebris` AdventureQuest. The picked giver
    /// asks the player to clear `ClearDebrisCount` resource clumps (logs / boulders /
    /// stumps / weeds clusters) at `ClearDebrisLocation` (default Pelican Town). Reward =
    /// `FriendshipMid` to the giver. The `ClearDebris` step rides the framework's per-second
    /// resource-clump poll (Phase 9.5c).
}
