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

    /// Picks a current-season seed via Data/Crops + the existing seed-resolver pattern
    /// from PierresStockUp. Returns null if no seasonal crops resolve (e.g. on saves
    /// where every seasonal crop's seed got removed by a content pack).

    /// CSV row 17. Daily-board single-step `ClearDebris` AdventureQuest. The picked giver
    /// asks the player to clear `ClearDebrisCount` resource clumps (logs / boulders /
    /// stumps / weeds clusters) at `ClearDebrisLocation` (default Pelican Town). Reward =
    /// `FriendshipMid` to the giver. The `ClearDebris` step rides the framework's per-second
    /// resource-clump poll (Phase 9.5c).
    private static QuestPosting? ClearDebris(QuestContext ctx)
    {
        var npcs = DispatchRegistry.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        string location = string.IsNullOrWhiteSpace(ModEntry.Config.ClearDebrisLocation)
            ? "Town"
            : ModEntry.Config.ClearDebrisLocation;
        int count = Math.Max(1, ModEntry.Config.ClearDebrisCount);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ClearDebris",
                Kind = AdventureStepKind.ClearDebris,
                Targets = new List<string> { location },
                Count = count,
                Description = ModEntry.I18n.Get("quest.foraging.clearDebris.step", new { count, location })
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
            Description = ModEntry.I18n.Get("quest.foraging.clearDebris.description", new { npc = giver, count, location }),
            CurrentObjective = ModEntry.I18n.Get("quest.foraging.clearDebris.objective", new { count, location }),
            TargetMessage = ModEntry.I18n.Get("quest.foraging.clearDebris.targetMessage"),
            PreBuiltQuest = quest
        };
    }

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

    /// CSV row 55. Daily-board single-step `Plant` AdventureQuest. Quest giver picked
    /// from the `ConservationGuide` dispatch role (Linus / Demetrius / Kimpoi RSV /
    /// Dylan ESV / Aster VMV) — exactly the CSV's listed givers. Player must plant
    /// `PlantTreesCount` trees at `PlantTreesLocation` (default Cindersap Forest).
    /// Reward = `FriendshipIntermediate` to the giver — pure friendship, no gold per
    /// the CSV. The `Plant` step rides `World.TerrainFeatureListChanged` filter Tree.
    private static QuestPosting? PlantTrees(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.ConservationGuide);
        if (giver == null)
            return null;

        string location = string.IsNullOrWhiteSpace(ModEntry.Config.PlantTreesLocation)
            ? "Forest"
            : ModEntry.Config.PlantTreesLocation;
        int count = Math.Max(1, ModEntry.Config.PlantTreesCount);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "PlantTrees",
                Kind = AdventureStepKind.Plant,
                Targets = new List<string> { location },
                Count = count,
                Description = ModEntry.I18n.Get("quest.foraging.plantTrees.step", new { count, location })
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
            Description = ModEntry.I18n.Get("quest.foraging.plantTrees.description", new { npc = giver, count, location }),
            CurrentObjective = ModEntry.I18n.Get("quest.foraging.plantTrees.objective", new { count, location }),
            TargetMessage = ModEntry.I18n.Get("quest.foraging.plantTrees.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// CSV row 69. Spring-only daily-board single-step `ClearWeeds` AdventureQuest. Any
    /// met human NPC can be the giver; the player clears `SpringCleaningCount` weed
    /// `Object`s at `SpringCleaningLocation` (default Pelican Town). Reward =
    /// `FriendshipBasic`. The `ClearWeeds` step rides `World.ObjectListChanged` filtered
    /// to `Object.IsWeeds()` removals.
}
