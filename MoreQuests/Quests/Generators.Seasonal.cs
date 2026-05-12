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
    private static QuestPosting? BeachCleanup(QuestContext ctx)
    {
        var pool = ctx.Items.GetBeachForageItems();
        if (pool.Count == 0)
            return null;
        var pick = pool[Game1.random.Next(pool.Count)];

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int foragingLevel = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            int upper = Math.Max(2, (int)(foragingLevel * 1.5));
            qty = Game1.random.Next(2, upper + 1);
        }
        else
        {
            qty = Game1.random.Next(2, 7);
        }

        string? giver = ctx.Dispatch.Pick(DispatchRoles.BeachCleanup);
        if (giver == null)
            return null;

        var quest = new CollectAndReportQuest
        {
            talkToNpc = { Value = giver },
            requiredCount = { Value = qty },
            reportMessage = { Value = ModEntry.I18n.Get("quest.seasonal.beach.targetMessage") }
        };
        quest.itemIds.Add(pick.QualifiedItemId);

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipMid) },
            Title = ModEntry.I18n.Get("quest.seasonal.beach.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.beach.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.beach.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.beach.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// CSV row 10 (rebranded from "SpringTea" to "FloralTea" in 9.5f). Year-round daily-board
    /// ItemDelivery. An adult human villager (not a tea-disliker) asks for a few of one
    /// seasonal flower they already love or like, so they can brew it into tea. The flower
    /// is sampled from the giver's own gift-taste row gated by the current-season context
    /// tag, so the request always lands on a flower the giver actually wants AND that's
    /// in season (winter rolls naturally fail to post in vanilla, which has no winter
    /// flowers; a modded winter flower with the right tag would let it run year-round).
    private static QuestPosting? FloralTea(QuestContext ctx)
    {
        var allFlowers = ctx.Items.GetItemsByCategory(StardewValley.Object.flowersCategory);
        string seasonTag = "season_" + ctx.Season.ToLowerInvariant();
        var seasonalFlowers = allFlowers
            .Where(f => f.ContextTags.Contains(seasonTag))
            .ToDictionary(f => f.QualifiedItemId, f => f, StringComparer.OrdinalIgnoreCase);
        if (seasonalFlowers.Count == 0)
            return null;

        var candidates = new List<string>(MetAdultHumanGiftReceivers());
        if (candidates.Count == 0)
            return null;
        // Shuffle so the eligibility scan doesn't always favour the first met NPC.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Game1.random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        string? chosenGiver = null;
        ResolvedItem? pick = null;
        foreach (var candidate in candidates)
        {
            if (NpcDislikesTea(ctx, candidate))
                continue;
            var flowerMatches = ResolveLovedOrLikedFlowers(ctx, candidate, seasonalFlowers);
            if (flowerMatches.Count == 0)
                continue;
            chosenGiver = candidate;
            pick = flowerMatches[Game1.random.Next(flowerMatches.Count)];
            break;
        }
        if (chosenGiver == null || pick == null)
            return null;

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int farming = Game1.player.FarmingLevel;
            int upper = Math.Max(2, (int)(farming * 1.5));
            qty = Game1.random.Next(2, upper + 1);
        }
        else
        {
            qty = Game1.random.Next(1, 6);
        }

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = chosenGiver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(chosenGiver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.seasonal.floraltea.title", new { npc = chosenGiver }),
            Description = ModEntry.I18n.Get("quest.seasonal.floraltea.description", new { npc = chosenGiver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.floraltea.objective", new { qty, item = pick.DisplayName, npc = chosenGiver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.floraltea.targetMessage")
        };
    }

    /// Walks the NPC's `Data/NPCGiftTastes` loved + liked lists and returns every flower
    /// (resolved against `seasonalFlowers`) that's in-season for this posting. Empty result
    /// means the giver has no seasonal flower preference and `FloralTea` skips them.
    private static List<ResolvedItem> ResolveLovedOrLikedFlowers(QuestContext ctx, string npc, IDictionary<string, ResolvedItem> seasonalFlowers)
    {
        var matches = new List<ResolvedItem>();
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var tasteData))
            return matches;
        var fields = tasteData.Split('/');
        if (fields.Length < 4)
            return matches;

        AppendFlowerMatches(fields[1], seasonalFlowers, matches); // loved
        AppendFlowerMatches(fields[3], seasonalFlowers, matches); // liked
        return matches;
    }

    private static void AppendFlowerMatches(string raw, IDictionary<string, ResolvedItem> pool, List<ResolvedItem> sink)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;
        foreach (var token in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out int n) && n < 0)
                continue;
            string qualified = token.StartsWith("(", StringComparison.Ordinal) ? token : "(O)" + token;
            if (pool.TryGetValue(qualified, out var resolved) && !sink.Contains(resolved))
                sink.Add(resolved);
        }
    }

    /// True if the NPC's disliked (field 5) or hated (field 7) gift tokens list Green Tea
    /// (object 614) or Tea Leaves (815). The taste data uses bare ids; both qualified and
    /// bare forms are tolerated so a modded gift-tastes edit that emits `(O)614` doesn't
    /// slip past. Modded tea items would need their own check; vanilla coverage is good
    /// enough for the FloralTea narrative gate.
    private static bool NpcDislikesTea(QuestContext ctx, string npc)
    {
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var raw))
            return false;
        var fields = raw.Split('/');
        if (fields.Length < 8)
            return false;
        return TokensContainTea(fields[5]) || TokensContainTea(fields[7]);
    }

    private static bool TokensContainTea(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        foreach (var token in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == "614" || token == "(O)614")
                return true;
            if (token == "815" || token == "(O)815")
                return true;
        }
        return false;
    }

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

    /// CSV row 37. Summer-only daily-board ItemDelivery. Asks for a cold-food vanilla
    /// staple (Ice Cream, Melon, or Juice) for a HeatWaveRelief-role giver (Harvey + Maru
    /// vanilla, Paula + Philip RSV, Jacob East Scarp). Reward = `FriendshipBasic` plus one
    /// random item pulled from Harvey's clinic shop (`Data/Shops["Hospital"]`); if the
    /// shop scan returns nothing (e.g. a content pack wiped the entry) the friendship
    /// reward stands alone.
    private static readonly string[] HeatWaveColdItemIds =
    {
        "(O)233", // Ice Cream
        "(O)254", // Melon
        "(O)350"  // Juice
    };

    private static QuestPosting? HeatWaveRelief(QuestContext ctx)
    {
        if (!string.Equals(ctx.Season, "summer", StringComparison.OrdinalIgnoreCase))
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.HeatWaveRelief);
        if (giver == null)
            return null;

        var coldItems = new List<ResolvedItem>(HeatWaveColdItemIds.Length);
        foreach (var id in HeatWaveColdItemIds)
        {
            var resolved = ctx.Items.TryResolveItem(id);
            if (resolved != null)
                coldItems.Add(resolved);
        }
        if (coldItems.Count == 0)
            return null;
        var pick = coldItems[Game1.random.Next(coldItems.Count)];

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            qty = Game1.random.Next(3, 11);
        }
        else
        {
            qty = Game1.random.Next(1, 6);
        }

        var rewards = new List<RewardSpec>
        {
            new FriendshipReward(giver, ctx.Config.FriendshipBasic)
        };
        var shopItems = ctx.Items.GetShopItems("Hospital");
        if (shopItems.Count > 0)
        {
            var rewardItem = shopItems[Game1.random.Next(shopItems.Count)];
            rewards.Add(new ObjectReward(rewardItem.QualifiedItemId));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.heatWaveRelief.targetMessage")
        };
    }

    /// Curated vanilla beach forageables. Mirrors the BeachForage pool used by
    /// BeachCleanup since both quests target the same kind of items.

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

    /// Walks `Data/Crops` for seeds whose Seasons list excludes Winter. Returns the
    /// resolved seed items so the picker can hand one to the Ship objective.

    /// CSV row 69. Spring-only daily-board single-step `ClearWeeds` AdventureQuest. Any
    /// met human NPC can be the giver; the player clears `SpringCleaningCount` weed
    /// `Object`s at `SpringCleaningLocation` (default Pelican Town). Reward =
    /// `FriendshipBasic`. The `ClearWeeds` step rides `World.ObjectListChanged` filtered
    /// to `Object.IsWeeds()` removals.
    private static QuestPosting? SpringCleaning(QuestContext ctx)
    {
        if (!string.Equals(ctx.Season, "spring", StringComparison.OrdinalIgnoreCase))
            return null;

        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        string location = string.IsNullOrWhiteSpace(ModEntry.Config.SpringCleaningLocation)
            ? "Town"
            : ModEntry.Config.SpringCleaningLocation;
        int count = Math.Max(1, ModEntry.Config.SpringCleaningCount);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ClearWeeds",
                Kind = AdventureStepKind.ClearWeeds,
                Targets = new List<string> { location },
                Count = count,
                Description = ModEntry.I18n.Get("quest.seasonal.springCleaning.step", new { count, location })
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.seasonal.springCleaning.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.seasonal.springCleaning.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.seasonal.springCleaning.description", new { npc = giver, count, location }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.springCleaning.objective", new { count, location }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.springCleaning.targetMessage"),
            PreBuiltQuest = quest
        };
    }
}
