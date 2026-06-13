using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Rewards;

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
        // straight payout reads as him cutting you in, and the real payoff is the arc
        // continuing (Step 2 mails you after this completes).
        posting.Rewards.Add(new MoneyReward(2000));
        return posting;
    }
}
