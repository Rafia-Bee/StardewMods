using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board (summer): collect beach foragables and report back. Player keeps the items.
/// Source: quest table row "Seasonal, Summer, Beach Cleanup".
internal sealed class BeachCleanup : IQuestDefinition
{
    public string Id => "Seasonal.BeachCleanup";
    public QuestCategory Category => QuestCategory.Seasonal;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 30;
    public int MaxPerDay => 1;
    public int CooldownDays => 7;

    private static readonly (string Id, string Name)[] BeachForage =
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

    public bool IsAvailable(QuestContext ctx) => ctx.Season == "summer";

    public QuestPosting? Build(QuestContext ctx)
    {
        var pick = BeachForage[Game1.random.Next(BeachForage.Length)];
        int qty = Game1.random.Next(2, 6);

        string? giver = NpcDispatch.Pick(ctx.Helper.ModRegistry, NpcDispatch.Role.BeachCleanup);
        if (giver == null)
            return null;

        var quest = new CollectAndReportQuest
        {
            talkToNpc = { Value = giver },
            requiredCount = { Value = qty },
            reportMessage = { Value = ctx.Helper.Translation.Get("quest.seasonal.beach.targetMessage") }
        };
        quest.itemIds.Add(pick.Id);

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = giver,
            ObjectiveItemId = pick.Id,
            ObjectiveItemName = pick.Name,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipBasic,
            FriendshipRewardNpc = giver,
            Title = ctx.Helper.Translation.Get("quest.seasonal.beach.title", new { npc = giver }),
            Description = ctx.Helper.Translation.Get("quest.seasonal.beach.description", new { npc = giver, qty, item = pick.Name }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.seasonal.beach.objective", new { qty, item = pick.Name, npc = giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.seasonal.beach.targetMessage"),
            PreBuiltQuest = quest
        };
    }
}
