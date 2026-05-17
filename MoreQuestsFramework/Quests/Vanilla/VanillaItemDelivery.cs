using StardewValley.Quests;

namespace MoreQuestsFramework.Quests.Vanilla;

internal sealed class VanillaItemDelivery : IQuestDefinition
{
    public string Id => "Vanilla.ItemDelivery";
    public QuestCategory Category => QuestCategory.Social;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 35;
    public int MaxPerDay => 5;
    public int CooldownDays => 1;

    public bool IsAvailable(QuestContext ctx) => true;

    public QuestPosting? Build(QuestContext ctx)
    {
        var quest = new ItemDeliveryQuest();
        try
        {
            quest.reloadDescription();
            quest.reloadObjective();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(quest.target.Value))
            return null;

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = quest.target.Value,
            Title = string.IsNullOrEmpty(quest.questTitle) ? "Delivery request" : quest.questTitle,
            Description = quest.questDescription ?? "",
            CurrentObjective = quest.currentObjective ?? "",
            DeadlineDays = 2,
            PreBuiltQuest = quest
        };
    }
}
