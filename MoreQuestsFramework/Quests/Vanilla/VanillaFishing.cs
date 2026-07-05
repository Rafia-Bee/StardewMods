using StardewValley.Quests;

namespace MoreQuestsFramework.Quests.Vanilla;

internal sealed class VanillaFishing : IQuestDefinition
{
    public string Id => "Vanilla.Fishing";
    public string Category => QuestCategory.Fishing;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 10;
    public int MaxPerDay => 1;
    public int CooldownDays => ModEntry.Config.CooldownShortDays;

    public bool IsAvailable(QuestContext ctx) => true;

    public QuestPosting? Build(QuestContext ctx)
    {
        var quest = new FishingQuest();
        try
        {
            quest.reloadDescription();
            quest.reloadObjective();
        }
        catch
        {
            return null;
        }

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = string.IsNullOrEmpty(quest.target.Value) ? "Willy" : quest.target.Value,
            Title = string.IsNullOrEmpty(quest.questTitle) ? "Fishing request" : quest.questTitle,
            Description = quest.questDescription ?? "",
            CurrentObjective = quest.currentObjective ?? "",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ModEntry.Config),
            PreBuiltQuest = quest
        };
    }
}
