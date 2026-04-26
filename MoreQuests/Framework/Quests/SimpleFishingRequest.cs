using System;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: catch X common seasonal fish.
/// Source: quest table row "Fishing, Basic Catch, Simple Fishing Request".
internal sealed class SimpleFishingRequest : IQuestDefinition
{
    public string Id => "Fishing.SimpleRequest";
    public QuestCategory Category => QuestCategory.Fishing;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 50;
    public int MaxPerDay => 1;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) => true;

    public QuestPosting? Build(QuestContext ctx)
    {
        var fish = ctx.Items.GetSeasonalFish(ctx.Season);
        if (fish.Count == 0)
            return null;

        fish.Sort((a, b) => a.Difficulty.CompareTo(b.Difficulty));
        var pool = fish.GetRange(0, Math.Min(fish.Count, Math.Max(3, fish.Count / 2)));
        var target = pool[Game1.random.Next(pool.Count)];

        int qty = Game1.random.Next(1, 4);
        int gold = Math.Clamp((int)(target.SellPrice * qty * 0.7), 100, 250);

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = "Willy",
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.fishing.simple.title"),
            Description = ctx.Helper.Translation.Get("quest.fishing.simple.description", new { qty, item = target.DisplayName }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.fishing.simple.objective", new { qty, item = target.DisplayName }),
            TargetMessage = ctx.Helper.Translation.Get("quest.fishing.simple.targetMessage")
        };
    }
}
