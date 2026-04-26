using StardewValley;
using StardewValley.Locations;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests.Vanilla;

/// Wraps vanilla `SlayMonsterQuest` (Adventurer's Guild monster eradication).
internal sealed class VanillaSlayMonster : IQuestDefinition
{
    public string Id => "Vanilla.SlayMonster";
    public QuestCategory Category => QuestCategory.Mining;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 12;
    public int MaxPerDay => 1;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) =>
        MineShaft.lowestLevelReached > 0 && Game1.stats.DaysPlayed > 5;

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
            DeadlineDays = 2,
            PreBuiltQuest = quest
        };
    }
}
