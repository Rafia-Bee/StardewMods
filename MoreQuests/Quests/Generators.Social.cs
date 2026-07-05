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

    /// Two-step Adventure: talk to N met villagers in any order (Talk step's CreditedKeys
    /// enforces uniqueness), then report back to the giver. Needs at least N+1 met villagers.
    /// Reward: FriendshipIntermediate to the giver.
    private static QuestPosting? CheckOnFriends(QuestContext ctx)
    {
        int n = Math.Max(1, ModEntry.Config.CheckOnFriendsCount);
        var metNpcs = MetAdultHumanGiftReceivers();
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

    /// Anonymous gift-delivery: a met NPC asks the player to drop a loved/liked item on a
    /// different met NPC. Reward: below-sell gold + FriendshipBasic to both NPCs.
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

        int gold = Math.Max(50, (int)(pick.SellPrice * ctx.Config.RewardMultiplierBelowSell));

        // Modded NPCs sometimes use namespaced names (e.g. "Nova.Eli"). Pull DisplayName so
        // the journal shows "Eli" not the raw prefix. Falls back to internal name if unresolved.
        string recipientDisplay = Game1.getCharacterFromName(recipient)?.displayName ?? recipient;

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            DeliveryTarget = recipient,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic),
                new FriendshipReward(recipient, ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.social.giftDelivery.title", new { recipient = recipientDisplay }),
            Description = ModEntry.I18n.Get("quest.social.giftDelivery.description", new { recipient = recipientDisplay, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.social.giftDelivery.objective", new { item = pick.DisplayName, recipient = recipientDisplay }),
            TargetMessage = ModEntry.I18n.Get("quest.social.giftDelivery.targetMessage")
        };
    }

    /// Dialogue-triggered Adventure quest from SVE's friendable Gunther (GuntherSilvian).
    /// Vanilla Gunther isn't a normal NPC (right-click opens the museum donation menu, not
    /// dialogue), so the framework's NpcDialogue trigger can never fire on him. SVE swaps
    /// him for GuntherSilvian who behaves like a regular speaker. The quest is gated on SVE
    /// being loaded; on a no-SVE save it never posts. One DonateMuseum step (any item
    /// suitable for the museum counts). Reward is a random furniture item from the
    /// Data/MuseumRewards pool, picked at posting time. Prefers items the player hasn't
    /// yet collected from the museum directly, but falls back to picking any furniture
    /// reward (the duplicate is harmless for a quest mail grant).
    private static QuestPosting? GuntherMuseumDonation(QuestContext ctx)
    {
        if (!ctx.Helper.ModRegistry.IsLoaded(ModCompat.StardewValleyExpanded))
            return null;
        if (Game1.getCharacterFromName("GuntherSilvian") == null)
            return null;
        // Museum is full, so no item the player could find is donatable. The DonateMuseum
        // step would be impossible and the quest would auto-fail on the deadline. Skip.
        int donated = Game1.netWorldState?.Value?.MuseumPieces?.Length ?? 0;
        if (donated >= StardewValley.Locations.LibraryMuseum.totalArtifacts)
            return null;

        string? rewardItemId = PickRandomMuseumDecorReward(ctx);
        if (rewardItemId == null)
            return null;
        var rewardItem = ctx.Items.TryResolveItem(rewardItemId);
        if (rewardItem == null)
            return null;

        const string giver = "GuntherSilvian";

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DonateOne",
                Kind = AdventureStepKind.DonateMuseum,
                Count = 1,
                Description = ModEntry.I18n.Get("quest.social.guntherMuseum.step.donate")
            },
            new AdventureStepState
            {
                Name = "ReportBack",
                Kind = AdventureStepKind.Talk,
                Count = 1,
                Requires = new List<string> { "DonateOne" },
                Description = ModEntry.I18n.Get("quest.social.guntherMuseum.step.report")
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.social.guntherMuseum.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new ObjectReward(rewardItem.QualifiedItemId, 1) },
            Title = ModEntry.I18n.Get("quest.social.guntherMuseum.title"),
            Description = ModEntry.I18n.Get("quest.social.guntherMuseum.description", new { item = rewardItem.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.social.guntherMuseum.step.donate"),
            TargetMessage = ModEntry.I18n.Get("quest.social.guntherMuseum.targetMessage"),
            DialogueText = ModEntry.I18n.Get("quest.social.guntherMuseum.description", new { item = rewardItem.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    private static string? PickRandomMuseumDecorReward(QuestContext ctx)
    {
        Dictionary<string, StardewValley.GameData.Museum.MuseumRewards> data;
        try
        {
            data = ctx.Helper.GameContent.Load<Dictionary<string, StardewValley.GameData.Museum.MuseumRewards>>("Data/MuseumRewards");
        }
        catch (Exception)
        {
            return null;
        }

        var pool = new List<string>();
        var freshPool = new List<string>();
        foreach (var entry in data.Values)
        {
            if (entry?.RewardItemId == null || entry.RewardItemIsRecipe)
                continue;
            if (!entry.RewardItemId.StartsWith("(F)", StringComparison.Ordinal))
                continue;
            pool.Add(entry.RewardItemId);
            var probe = StardewValley.ItemRegistry.Create(entry.RewardItemId, 1, 0, allowNull: true);
            if (probe == null)
                continue;
            // Must match LibraryMuseum.getRewardItemKey exactly; that vanilla method still
            // calls Utility.getStandardDescriptionFromItem so we follow suit.
#pragma warning disable CS0618
            string key = "museumCollectedReward" + StardewValley.Utility.getStandardDescriptionFromItem(probe, 1, '_');
#pragma warning restore CS0618
            if (Game1.player == null || !Game1.player.mailReceived.Contains(key))
                freshPool.Add(entry.RewardItemId);
        }

        var chosen = freshPool.Count > 0 ? freshPool : pool;
        if (chosen.Count == 0)
            return null;
        return chosen[Game1.random.Next(chosen.Count)];
    }

    /// OneShot mail quest, fired once the player hits 5 hearts with Emily (the
    /// "FirstFriendship Emily >= 5" trigger in quests.json). Emily asks the player to
    /// liven up the farmhouse: place a number of furniture pieces (config) while the quest
    /// is active, including a rug, a light source, and a wall decoration, then talk to her.
    /// The Decorate step only counts furniture placed after the quest starts, so a player
    /// who already decorated can't auto-complete it. Reward: a random dresser of the kind
    /// Robin sells, plus FriendshipMid with Emily.
    private static QuestPosting? EmilyHousewarming(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Emily") == null)
            return null;

        string? dresserId = PickRandomDresser(ctx);
        if (dresserId == null)
            return null;
        var dresser = ctx.Items.TryResolveItem(dresserId);
        if (dresser == null)
            return null;

        const string giver = "Emily";
        int total = Math.Max(3, ModEntry.Config.EmilyHousewarmingCount);
        int other = total - 3;

        var steps = new List<AdventureStepState>
        {
            DecorateStep("PlaceLight", "light", 1, "quest.social.emilyHousewarming.step.light"),
            DecorateStep("PlaceRug", "rug", 1, "quest.social.emilyHousewarming.step.rug"),
            DecorateStep("PlaceWall", "wall", 1, "quest.social.emilyHousewarming.step.wall")
        };
        var requires = new List<string> { "PlaceLight", "PlaceRug", "PlaceWall" };
        if (other > 0)
        {
            steps.Add(new AdventureStepState
            {
                Name = "PlaceOther",
                Kind = AdventureStepKind.Decorate,
                Targets = new List<string> { "FarmHouse" },
                Items = new List<string> { "other" },
                Count = other,
                Description = ModEntry.I18n.Get("quest.social.emilyHousewarming.step.other")
            });
            requires.Add("PlaceOther");
        }
        steps.Add(new AdventureStepState
        {
            Name = "TalkEmily",
            Kind = AdventureStepKind.Talk,
            Targets = new List<string> { giver },
            Requires = requires,
            Count = 1,
            Description = ModEntry.I18n.Get("quest.social.emilyHousewarming.step.talk")
        });

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.social.emilyHousewarming.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            // Extended tier (14 days by default): a missed one-shot never comes back, so
            // give a generous window.
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Extended, ctx.Config),
            Rewards =
            {
                new ObjectReward(dresser.QualifiedItemId, 1),
                new FriendshipReward(giver, ctx.Config.FriendshipMid)
            },
            Title = ModEntry.I18n.Get("quest.social.emilyHousewarming.title"),
            Description = ModEntry.I18n.Get("quest.social.emilyHousewarming.description", new { count = total }),
            CurrentObjective = ModEntry.I18n.Get("quest.social.emilyHousewarming.step.light"),
            TargetMessage = ModEntry.I18n.Get("quest.social.emilyHousewarming.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    private static AdventureStepState DecorateStep(string name, string category, int count, string descKey) =>
        new AdventureStepState
        {
            Name = name,
            Kind = AdventureStepKind.Decorate,
            Targets = new List<string> { "FarmHouse" },
            Items = new List<string> { category },
            Count = count,
            Description = ModEntry.I18n.Get(descKey)
        };

    // Random dresser-type furniture (Data/Furniture Type field == "dresser"), the kind
    // Robin stocks at the Carpenter shop. Returns a (F)-qualified id, or null if none.
    private static string? PickRandomDresser(QuestContext ctx)
    {
        Dictionary<string, string> data;
        try
        {
            data = ctx.Helper.GameContent.Load<Dictionary<string, string>>("Data/Furniture");
        }
        catch (Exception)
        {
            return null;
        }

        var pool = new List<string>();
        foreach (var (key, raw) in data)
        {
            if (string.IsNullOrEmpty(raw))
                continue;
            var fields = raw.Split('/');
            if (fields.Length < 2)
                continue;
            if (!string.Equals(fields[1], "dresser", StringComparison.OrdinalIgnoreCase))
                continue;
            pool.Add("(F)" + key);
        }
        if (pool.Count == 0)
            return null;
        return pool[Game1.random.Next(pool.Count)];
    }
}
