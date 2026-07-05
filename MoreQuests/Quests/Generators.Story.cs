using System.Collections.Generic;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewValley;

namespace MoreQuests.Quests;

// The Joja/Pierre rivalry arc. Hand-authored story quests, chained by mail. Registered
// ungated (not through Reg) so a player turning off random Social quests doesn't lose
// the storyline.
internal static partial class Generators
{
    // Step 1, "A man of means". Morris mails you once you've shown some ambition (the
    // OneShot "FirstMoneyEarned >= 15000" trigger gates the letter, and the quest is only
    // offered while the Community Center isn't finished, since Morris leaves town after
    // that). No task, just a threshold: earn another 10,000g from when the letter lands.
    private const int MorrisManOfMeansGoldTarget = 10000;

    private static QuestPosting? MorrisManOfMeans(QuestContext ctx)
    {
        var posting = new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.EarnMoney,
            QuestGiver = "Morris",
            ObjectiveQuantity = MorrisManOfMeansGoldTarget,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.None, ctx.Config),
            Title = ModEntry.I18n.Get("quest.story.morrisManOfMeans.title"),
            Description = ModEntry.I18n.Get("quest.story.morrisManOfMeans.description",
                new { gold = MorrisManOfMeansGoldTarget }),
            CurrentObjective = ModEntry.I18n.Get("quest.story.morrisManOfMeans.objective")
        };

        // Morris isn't a social-tab NPC, so a friendship reward would be invisible. A
        // straight payout reads as him cutting you in. The handoff to Step 2 happens when
        // this quest completes (ModEntry.UnlockMorrisQualityControl), not through a letter.
        posting.Rewards.Add(new MoneyReward(2 * ctx.Config.GoldAdvancedBase));
        return posting;
    }

    // Step 2, "Quality control". Morris's real ask: make Pierre look bad by getting cheap
    // junk produce onto his shelves. Sell a batch of dirt-cheap, base-quality crops into
    // Pierre's shop (SeedShop). The framework's Sell objective counts them as they cross
    // the counter. Mailed to you once you've read the Step 1 follow-up letter.
    private const int MorrisQualityControlSellCount = 15;

    private static QuestPosting? MorrisQualityControl(QuestContext ctx)
    {
        int maxPrice = System.Math.Max(1, ModEntry.Config.MorrisQualityControlMaxCropPrice);
        var posting = new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Sell,
            QuestGiver = "Morris",
            SellShopId = "SeedShop",
            SellMaxValue = maxPrice,
            SellMaxQuality = 0,
            ObjectiveQuantity = MorrisQualityControlSellCount,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.None, ctx.Config),
            Title = ModEntry.I18n.Get("quest.story.morrisQualityControl.title"),
            Description = ModEntry.I18n.Get("quest.story.morrisQualityControl.description",
                new { count = MorrisQualityControlSellCount, value = maxPrice }),
            CurrentObjective = ModEntry.I18n.Get("quest.story.morrisQualityControl.objective")
        };

        // Cheap base-quality produce: vegetables, fruit, and flowers under the price cap.
        // Covers the obvious dumping crops (Wheat, Hops, and the like all sit in Vegetable).
        posting.SellCategories.Add(Object.VegetableCategory);
        posting.SellCategories.Add(Object.FruitsCategory);
        posting.SellCategories.Add(Object.flowersCategory);

        posting.Rewards.Add(new MoneyReward(ctx.Config.GoldExpertBase));
        return posting;
    }

    // "Don't get caught". Pierre's counter-prank against Morris: break into Joja after
    // midnight and set the place up to look like it's running a sad, cheap sale. The whole
    // job is mailed at once, so all four objectives show in the journal the moment you accept,
    // and each Requires the one before it. The beats are driven by content-side patches and
    // polls (see ModEntry.Pierre* handlers): the break-in opens the locked Joja door between
    // 12am and 2am, the shelves become deposit boxes for cheap pickles, the sign watches for
    // a stamped pickle sign placed outside Joja, and the lay-low finish resolves overnight.
    internal const int PierreStockPickleCount = 12;

    private static QuestPosting? PierreDontGetCaught(QuestContext ctx)
    {
        const string giver = "Pierre";

        var steps = new List<AdventureStepState>
        {
            new()
            {
                Name = "BreakIn",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.PierreBreakInHandler },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.step.breakIn")
            },
            new()
            {
                Name = "Stock",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.PierreStockHandler },
                Count = PierreStockPickleCount,
                Requires = new List<string> { "BreakIn" },
                Description = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.step.stock",
                    new { count = PierreStockPickleCount })
            },
            new()
            {
                Name = "CraftSign",
                Kind = AdventureStepKind.Craft,
                Items = new List<string> { "tag:sign_item" },
                Count = 1,
                Requires = new List<string> { "Stock" },
                Description = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.step.craftSign")
            },
            new()
            {
                Name = "Sign",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.PierreSignHandler },
                Count = 1,
                Requires = new List<string> { "CraftSign" },
                Description = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.step.sign")
            },
            new()
            {
                Name = "LayLow",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.PierreLayLowHandler },
                Count = 1,
                Requires = new List<string> { "Sign" },
                Description = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.step.layLow")
            }
        };

        // No completion dialogue: the quest finishes on sleep, so there's no talk-to-Pierre
        // turn-in where a message would ever show.
        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Social,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.None, ctx.Config),
            Rewards =
            {
                new MoneyReward(ctx.Config.GoldExpertBase),
                new FriendshipReward("Pierre", ctx.Config.FriendshipLarge)
            },
            Title = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.title"),
            Description = ModEntry.I18n.Get("quest.story.pierreDontGetCaught.description",
                new { count = PierreStockPickleCount }),
            PreBuiltQuest = quest
        };
    }
}
