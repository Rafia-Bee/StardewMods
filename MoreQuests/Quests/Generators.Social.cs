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
            Rewards = { new FriendshipReward("Elliott", ctx.Config.FriendshipLarge) },
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
}
