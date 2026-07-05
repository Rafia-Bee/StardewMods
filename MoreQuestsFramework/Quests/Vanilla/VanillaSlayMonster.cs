using MoreQuestsFramework.Conditions;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests.Vanilla;

internal sealed class VanillaSlayMonster : IQuestDefinition
{
    public string Id => "Vanilla.SlayMonster";
    public string Category => QuestCategory.Mining;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 12;
    public int MaxPerDay => 1;
    public int CooldownDays => ModEntry.Config.CooldownShortDays;

    public bool IsAvailable(QuestContext ctx) =>
        ConditionEvaluator.MineShaftReached(1) && ConditionEvaluator.MinDaysPlayed(5);

    public QuestPosting? Build(QuestContext ctx)
    {
        var quest = new SlayMonsterQuest();
        quest.ignoreFarmMonsters.Value = true;
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
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = string.IsNullOrEmpty(quest.target.Value) ? "Marlon" : quest.target.Value,
            Title = string.IsNullOrEmpty(quest.questTitle) ? "Monster eradication" : quest.questTitle,
            Description = quest.questDescription ?? "",
            CurrentObjective = quest.currentObjective ?? "",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ModEntry.Config),
            PreBuiltQuest = quest
        };
    }
}
