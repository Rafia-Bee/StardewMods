using System.Collections.Generic;
using System.Linq;
using MoreQuests.Framework.Conditions;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board (fall): ship spring flowers.
/// Source: quest table row "Seasonal, Fall, Spring Tea".
internal sealed class SpringTea : IQuestDefinition
{
    public string Id => "Seasonal.SpringTea";
    public QuestCategory Category => QuestCategory.Seasonal;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 40;
    public int MaxPerDay => 1;
    public int CooldownDays => 8;

    public bool IsAvailable(QuestContext ctx) => ConditionEvaluator.MatchesSeason("fall");

    public QuestPosting? Build(QuestContext ctx)
    {
        var allFlowers = ctx.Items.GetItemsByCategory(StardewValley.Object.flowersCategory);
        var springFlowers = allFlowers
            .Where(f => f.ContextTags.Contains("season_spring"))
            .ToList();

        if (springFlowers.Count == 0)
            return null;

        var pick = springFlowers[Game1.random.Next(springFlowers.Count)];
        int qty = Game1.random.Next(3, 6);

        var npcs = NpcDispatch.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipBasic,
            FriendshipRewardNpc = giver,
            Title = ctx.Helper.Translation.Get("quest.seasonal.springtea.title", new { npc = giver }),
            Description = ctx.Helper.Translation.Get("quest.seasonal.springtea.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.seasonal.springtea.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.seasonal.springtea.targetMessage")
        };
    }
}
