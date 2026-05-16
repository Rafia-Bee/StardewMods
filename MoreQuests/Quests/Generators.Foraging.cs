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
    private static QuestPosting? SeasonalForaging(QuestContext ctx)
    {
        var pool = ctx.Config.ForagingIgnoresVisitedLocations
            ? ctx.Items.GetForageItems(ctx.Season)
            : ctx.Items.GetForageItemsInVisitedLocations(ctx.Season);
        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];
        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int foragingLevel = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            int upper = Math.Max(3, (int)(foragingLevel * 1.5));
            qty = Game1.random.Next(3, upper + 1);
        }
        else
        {
            qty = Game1.random.Next(2, 8);
        }
        int gold = ctx.Config.GoldBeginnerBase;

        var npcs = MetAdultHumanGiftReceivers();
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

    /// Linus asks the player to gift forage to a few distinct NPCs (who love/like it).
    /// Reward: FriendshipLarge with Linus. Recipients get the standard vanilla gift bump.
    private static QuestPosting? ForageWithLinus(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Linus") == null)
            return null;

        int recipientCount;
        if (ctx.Config.DifficultyScaling)
        {
            int foragingLevel = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            int upper = Math.Max(3, foragingLevel);
            recipientCount = Game1.random.Next(3, upper + 1);
        }
        else
        {
            recipientCount = Game1.random.Next(2, 7);
        }

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "GiftForage",
                Kind = AdventureStepKind.GiftUniqueNpcs,
                // Empty Targets = any villager. Handler enforces "loved/liked by recipient"
                // + "$forage tagged item" at gift-time.
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

    /// Vanilla rare forageables. Modded forage gets appended via the `forage_item` tag.
    private static readonly (string Id, string Name)[] RareForagePool =
    {
        ("(O)394", "Rainbow Shell"),
        ("(O)88", "Cactus Fruit"),
        ("(O)851", "Magma Cap")
    };

    /// ItemDelivery: a met NPC asks for a small stack of rare forage.
    /// Reward: GoldIntermediateBase + 10 of one current-season seed.
    private static QuestPosting? RareForageHunt(QuestContext ctx)
    {
        var metNpcs = MetAdultHumanGiftReceivers();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var pool = ResolveRareForage(ctx);
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
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var seedReward = PickSeasonalSeed(ctx);
        if (seedReward != null)
            rewards.Add(new ObjectReward(seedReward.QualifiedItemId, 2 * qty));

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

    /// Vanilla rare forage + items tagged `forage_item` and NOT `season_&lt;current&gt;`
    /// (so the posting feels rare, not just an expanded SeasonalForaging).
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

        // Append modded forage that isn't tagged with the current season. Current-season
        // forage belongs to BasicForaging; the rare hunt should actually feel rare.
        string currentSeasonTag = "season_" + ctx.Season.ToLowerInvariant();
        var allForage = ctx.Config.ForagingIgnoresVisitedLocations
            ? ctx.Items.GetForageItems()
            : ctx.Items.GetForageItemsInVisitedLocations();
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

    /// Single-step ClearDebris AdventureQuest: an adult human asks the player to clear
    /// 5..20 resource clumps anywhere except the farm. Reward: FriendshipMid.
    private static QuestPosting? ClearDebris(QuestContext ctx)
    {
        var npcs = MetAdultHumanGiftReceivers();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        int count = Game1.random.Next(5, 21);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ClearDebris",
                Kind = AdventureStepKind.ClearDebris,
                Targets = new List<string> { "*", "!Farm" },
                Count = count,
                Description = ModEntry.I18n.Get("quest.foraging.clearDebris.step", new { count })
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.foraging.clearDebris.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Foraging,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipMid) },
            Title = ModEntry.I18n.Get("quest.foraging.clearDebris.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.foraging.clearDebris.description", new { npc = giver, count }),
            CurrentObjective = ModEntry.I18n.Get("quest.foraging.clearDebris.objective", new { count }),
            TargetMessage = ModEntry.I18n.Get("quest.foraging.clearDebris.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Single-step Plant AdventureQuest from a ConservationGuide role NPC. Player plants a
    /// scaled number of trees anywhere outside the farm. Reward: FriendshipIntermediate.
    /// PlantTreesPatches opens the CanPlantTreesHere gate on every non-Farm location while
    /// the quest is active.
    private static QuestPosting? PlantTrees(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.ConservationGuide);
        if (giver == null)
            return null;

        int count;
        if (ctx.Config.DifficultyScaling)
        {
            int foragingLevel = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            int upper = Math.Max(3, foragingLevel);
            count = Game1.random.Next(3, upper + 1);
        }
        else
        {
            count = Game1.random.Next(2, 7);
        }

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "PlantTrees",
                Kind = AdventureStepKind.Plant,
                Targets = new List<string> { "*", "!Farm" },
                Count = count,
                Description = ModEntry.I18n.Get("quest.foraging.plantTrees.step", new { count })
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.foraging.plantTrees.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Foraging,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipIntermediate) },
            Title = ModEntry.I18n.Get("quest.foraging.plantTrees.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.foraging.plantTrees.description", new { npc = giver, count }),
            CurrentObjective = ModEntry.I18n.Get("quest.foraging.plantTrees.objective", new { count }),
            TargetMessage = ModEntry.I18n.Get("quest.foraging.plantTrees.targetMessage"),
            PreBuiltQuest = quest
        };
    }
}
