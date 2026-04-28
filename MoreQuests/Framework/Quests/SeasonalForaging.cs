using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: gather and ship X seasonal forage items.
/// Source: quest table row "Foraging, Basic Gather, Seasonal Foraging".
internal sealed class SeasonalForaging : IQuestDefinition
{
    public string Id => "Foraging.Seasonal";
    public QuestCategory Category => QuestCategory.Foraging;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 50;
    public int MaxPerDay => 3;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) =>
        ctx.Season is "spring" or "summer" or "fall" or "winter";

    public QuestPosting? Build(QuestContext ctx)
    {
        var pool = ctx.Items.GetForageItems(ctx.Season);
        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];
        int qty = Game1.random.Next(3, 8);
        int gold = ctx.Config.GoldBeginnerBase;

        var npcs = NpcDispatch.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;
        string giver = npcs[Game1.random.Next(npcs.Count)];

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.foraging.seasonal.title", new { npc = giver }),
            Description = ctx.Helper.Translation.Get("quest.foraging.seasonal.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.foraging.seasonal.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.foraging.seasonal.targetMessage")
        };
    }
}
