using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: slay X slimes (any variant) in the mines. Posted by Marlon.
/// Source: quest table row "Mining, Combat, Basic Slime Clearing".
internal sealed class BasicSlimeClearing : IQuestDefinition
{
    public string Id => "Mining.BasicSlimeClearing";
    public QuestCategory Category => QuestCategory.Mining;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 50;
    public int MaxPerDay => 1;
    public int CooldownDays => 7;

    public bool IsAvailable(QuestContext ctx) => Game1.player.deepestMineLevel > 0;

    public QuestPosting? Build(QuestContext ctx)
    {
        int qty = Game1.random.Next(8, 16);
        int gold = ctx.Config.GoldBeginnerBase + Game1.random.Next(0, 100);

        var quest = new AnySlimeQuest
        {
            target = { Value = "Marlon" },
            monsterName = { Value = "Green Slime" },
            numberToKill = { Value = qty },
            targetMessage = ctx.Helper.Translation.Get("quest.mining.slime.targetMessage")
        };

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = "Marlon",
            ObjectiveItemId = "Green Slime",
            ObjectiveItemName = "Green Slime",
            ObjectiveQuantity = qty,
            TargetMonster = "Green Slime",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.mining.slime.title"),
            Description = ctx.Helper.Translation.Get("quest.mining.slime.description", new { qty }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.mining.slime.objective", new { qty }),
            TargetMessage = ctx.Helper.Translation.Get("quest.mining.slime.targetMessage"),
            PreBuiltQuest = quest
        };
    }
}
