using System;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Periodic board posting: deliver hay to Marnie. Quantity scales with the number of farm animals.
/// Source: quest table row "Animal, Ongoing, Hay Supply Run".
internal sealed class HaySupplyRun : IQuestDefinition
{
    public string Id => "Animal.HaySupplyRun";
    public QuestCategory Category => QuestCategory.Animal;
    public PostingKind Kind => PostingKind.Mail;
    public int DefaultWeight => 0;
    public int MaxPerDay => 1;
    public int CooldownDays => 28;

    public bool IsAvailable(QuestContext ctx)
    {
        if (!ctx.Config.AnimalQuestsEnabled)
            return false;
        return CountAnimals() >= 4;
    }

    public QuestPosting? Build(QuestContext ctx)
    {
        int animals = CountAnimals();
        int qty = Math.Max(ctx.Config.HaySupplyBaseQty, animals * 3);
        int gold = (int)(qty * 50 * 0.8);

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)178",
            ObjectiveItemName = "Hay",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.animal.hay.title"),
            Description = ctx.Helper.Translation.Get("quest.animal.hay.description", new { qty }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.animal.hay.objective", new { qty }),
            TargetMessage = ctx.Helper.Translation.Get("quest.animal.hay.targetMessage")
        };
    }

    private static int CountAnimals()
    {
        int total = 0;
        foreach (var location in Game1.locations)
        {
            total += location.animals.Count();
            foreach (var building in location.buildings)
            {
                var indoor = building.GetIndoors();
                if (indoor != null)
                    total += indoor.animals.Count();
            }
        }
        return total;
    }
}
