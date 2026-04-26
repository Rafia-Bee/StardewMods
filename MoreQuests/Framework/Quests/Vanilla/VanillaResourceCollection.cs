using StardewValley.Quests;

namespace MoreQuests.Framework.Quests.Vanilla;

/// Wraps vanilla `ResourceCollectionQuest` (random NPC asks for X of a basic resource).
internal sealed class VanillaResourceCollection : IQuestDefinition
{
    public string Id => "Vanilla.ResourceCollection";
    public QuestCategory Category => QuestCategory.Foraging;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 15;
    public int MaxPerDay => 1;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) => true;

    public QuestPosting? Build(QuestContext ctx)
    {
        var quest = new ResourceCollectionQuest();
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
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = quest.target.Value,
            Title = string.IsNullOrEmpty(quest.questTitle) ? "Resource collection" : quest.questTitle,
            Description = quest.questDescription ?? "",
            CurrentObjective = quest.currentObjective ?? "",
            DeadlineDays = 2,
            PreBuiltQuest = quest
        };
    }
}
