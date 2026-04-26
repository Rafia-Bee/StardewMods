using StardewValley.Quests;

namespace MoreQuests.Framework.Quests.Vanilla;

/// Wraps vanilla `FishingQuest` (Willy asks for a specific fish catch).
internal sealed class VanillaFishing : IQuestDefinition
{
    public string Id => "Vanilla.Fishing";
    public QuestCategory Category => QuestCategory.Fishing;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 10;
    public int MaxPerDay => 1;
    public int CooldownDays => 2;

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
            DeadlineDays = 2,
            PreBuiltQuest = quest
        };
    }
}
