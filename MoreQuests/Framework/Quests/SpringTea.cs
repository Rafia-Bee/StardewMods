using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board (spring): ship spring flowers.
/// Source: quest table row "Seasonal, Spring, Spring Tea".
internal sealed class SpringTea : IQuestDefinition
{
    public string Id => "Seasonal.SpringTea";
    public QuestCategory Category => QuestCategory.Seasonal;
    public PostingKind Kind => PostingKind.DailyBoard;

    private static readonly (string Id, string Name)[] SpringFlowers =
    {
        ("(O)591", "Tulip"),
        ("(O)597", "Blue Jazz"),
        ("(O)376", "Poppy"),
        ("(O)595", "Fairy Rose")
        // TODO: make this dynamic for modded flowers.
    };

    public bool IsAvailable(QuestContext ctx) => ctx.Season == "spring";

    public QuestPosting? Build(QuestContext ctx)
    {
        var pick = SpringFlowers[Game1.random.Next(SpringFlowers.Length)];
        int qty = Game1.random.Next(3, 6);

        var npcs = NpcDispatch.MetHumanNpcs();
        string giver = npcs.Count > 0 ? npcs[Game1.random.Next(npcs.Count)] : "Caroline";

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.Id,
            ObjectiveItemName = pick.Name,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipBasic,
            FriendshipRewardNpc = giver,
            Title = ctx.Helper.Translation.Get("quest.seasonal.springtea.title", new { npc = giver }),
            Description = ctx.Helper.Translation.Get("quest.seasonal.springtea.description", new { npc = giver, qty, item = pick.Name }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.seasonal.springtea.objective", new { qty, item = pick.Name, npc = giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.seasonal.springtea.targetMessage")
        };
    }
}
