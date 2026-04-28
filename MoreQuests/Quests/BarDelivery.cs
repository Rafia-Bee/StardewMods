using System;
using MoreQuestsFramework;
using MoreQuestsFramework.Conditions;
using StardewValley;

namespace MoreQuests.Quests;

/// Daily board: deliver X metal bars to Clint.
/// Source: quest table row "Mining, Resource, Bar Delivery".
internal sealed class BarDelivery : IQuestDefinition
{
    public string Id => "Mining.BarDelivery";
    public QuestCategory Category => QuestCategory.Mining;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 35;
    public int MaxPerDay => 1;
    public int CooldownDays => 5;

    private static readonly (string Id, string Name)[] BarPool =
    {
        ("(O)334", "Copper Bar"),
        ("(O)335", "Iron Bar"),
        ("(O)336", "Gold Bar"),
        ("(O)337", "Iridium Bar")
    };

    public bool IsAvailable(QuestContext ctx) =>
        ConditionEvaluator.MiningLevel >= 1 && ConditionEvaluator.MinDeepestMineLevel(40);

    public QuestPosting? Build(QuestContext ctx)
    {
        int level = Game1.player.MiningLevel;
        bool skullCavernUnlocked = Game1.player.deepestMineLevel > 120;

        int maxIdxExclusive = skullCavernUnlocked ? 4 : 3;
        int barIdx = level switch
        {
            >= 8 => Game1.random.Next(2, maxIdxExclusive),
            >= 4 => Game1.random.Next(1, Math.Min(3, maxIdxExclusive)),
            _ => 0
        };
        var bar = BarPool[barIdx];

        int qty = Game1.random.Next(2, 5);
        int gold = ctx.Config.GoldIntermediateBase;

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Clint",
            ObjectiveItemId = bar.Id,
            ObjectiveItemName = bar.Name,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold, // TODO: reward should be gold + a geode or gem.
            Title = ModEntry.I18n.Get("quest.mining.bar.title"),
            Description = ModEntry.I18n.Get("quest.mining.bar.description", new { qty, item = bar.Name }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.bar.objective", new { qty, item = bar.Name }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.bar.targetMessage")
        };
    }
}
