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

    /// An adult villager (non-tea-disliker) asks for a few of one seasonal flower they
    /// love/like. Flower sampled from the giver's gift-taste row, gated by `season_&lt;current&gt;`,
    /// so the request lands on a flower the giver actually wants AND is in-season. Winter
    /// naturally fails in vanilla (no winter flowers); a modded one with the tag enables it.
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

    /// Returns every flower from the NPC's loved+liked lists that's in-season. Empty result
    /// means no seasonal flower preference, so FloralTea skips them.
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

    /// True if the NPC's disliked or hated gift tokens list Green Tea or Tea Leaves.
    /// Both qualified and bare forms tolerated. Modded tea items aren't covered.
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

    /// Summer-only ItemDelivery: a cold-food staple (Ice Cream, Melon, or Juice) for a
    /// HeatWaveRelief-role giver. Reward: FriendshipBasic + a random item from the Hospital shop.
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

    /// DateLocked Summer 21 (7 days before Moonlight Jellies on Summer 28) with a 6-day
    /// deadline. An EcologyMinded giver wants marine-life data before the dance. Forage pool
    /// reuses GetBeachForageItems. Reward: FriendshipBasic + a random loved/liked item from
    /// the giver's gift tastes.
    private static QuestPosting? JellyfishWatchPrep(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        var pool = ctx.Items.GetBeachForageItems();
        if (pool.Count == 0)
            return null;
        var pick = pool[Game1.random.Next(pool.Count)];

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int foragingLevel = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            qty = 2 + (foragingLevel / 2);
        }
        else
        {
            qty = Game1.random.Next(1, 5);
        }

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
            DeadlineDays = 6,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.title"),
            Description = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.description", new { qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.jellyfishWatch.targetMessage")
        };
    }

    // Lightning Rod is big-craftable id 9. Placed rods report this QualifiedItemId.
    private const string LightningRodQualifiedId = "(BC)9";

    /// Mail, fires the day before a storm (WeatherForecast trigger in quests.json). A
    /// ConservationGuide giver warns about the incoming storm and asks the player to get
    /// lightning rods up on the farm before it hits. Single Custom step tracked by a farm
    /// sweep in ModEntry.OnOneSecondTick. Rods already standing when the quest posts are
    /// pre-credited so only new placements count. Gated on knowing the Lightning Rod recipe
    /// (also enforced in quests.json Available). Reward: a battery pack mailed the next day.
    private static QuestPosting? BattenDownTheHatches(QuestContext ctx)
    {
        if (!(Game1.player?.craftingRecipes?.ContainsKey("Lightning Rod") ?? false))
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.ConservationGuide);
        if (giver == null)
            return null;

        int count;
        if (ctx.Config.DifficultyScaling)
        {
            int foraging = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            count = Game1.random.Next(2, Math.Max(1, foraging / 2));
        }
        else
        {
            count = Game1.random.Next(1, 4);
        }

        var step = new AdventureStepState
        {
            Name = "PlaceRods",
            Kind = AdventureStepKind.Custom,
            Targets = new List<string> { ModEntry.BattenDownStepHandler },
            Items = new List<string> { LightningRodQualifiedId },
            Count = count,
            CreditedKeys = ExistingFarmRodTileKeys(),
            Description = ModEntry.I18n.Get("quest.seasonal.battenDown.step", new { count })
        };

        var quest = new AdventureQuest();
        quest.Initialize(new[] { step }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.seasonal.battenDown.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Seasonal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FriendshipReward(giver, ctx.Config.FriendshipBasic),
                new MailReward(ModEntry.BattenDownRewardMailKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.seasonal.battenDown.title"),
            Description = ModEntry.I18n.Get("quest.seasonal.battenDown.description", new { count }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.battenDown.objective", new { count }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.battenDown.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Tile keys (same `Farm|x|y` format the poll uses) for every lightning rod already on
    /// the farm. Seeding these into the step's CreditedKeys means pre-existing rods never
    /// count, so the quest only credits rods the player puts up after it posts.
    internal static List<string> ExistingFarmRodTileKeys()
    {
        var keys = new List<string>();
        var farm = Game1.getFarm();
        if (farm?.Objects == null)
            return keys;
        foreach (var pair in farm.Objects.Pairs)
        {
            if (pair.Value?.QualifiedItemId == LightningRodQualifiedId)
                keys.Add($"Farm|{(int)pair.Key.X}|{(int)pair.Key.Y}");
        }
        return keys;
    }

    /// Spring-only single-step ClearWeeds AdventureQuest. Any met human can be the giver;
    /// the player clears SpringCleaningCount weeds anywhere except the farm. Wildcard
    /// targets avoid the "Town has no weeds today" dead-end. Reward: FriendshipBasic.
    private static QuestPosting? SpringCleaning(QuestContext ctx)
    {
        if (!string.Equals(ctx.Season, "spring", StringComparison.OrdinalIgnoreCase))
            return null;

        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        int count = Math.Max(1, ModEntry.Config.SpringCleaningCount);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ClearWeeds",
                Kind = AdventureStepKind.ClearWeeds,
                Targets = new List<string> { "*", "!Farm" },
                Count = count,
                Description = ModEntry.I18n.Get("quest.seasonal.springCleaning.step", new { count })
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
            Description = ModEntry.I18n.Get("quest.seasonal.springCleaning.description", new { npc = giver, count }),
            CurrentObjective = ModEntry.I18n.Get("quest.seasonal.springCleaning.objective", new { count }),
            TargetMessage = ModEntry.I18n.Get("quest.seasonal.springCleaning.targetMessage"),
            PreBuiltQuest = quest
        };
    }
}
