using StardewValley.Quests;

namespace MoreQuests.Framework.Quests.Vanilla;

/// Wraps vanilla `ItemDeliveryQuest`. Vanilla picks a random NPC + a random item
/// they like; we don't override that logic, only the chance of this type appearing.
internal sealed class VanillaItemDelivery : IQuestDefinition
{
    public string Id => "Vanilla.ItemDelivery";
    public QuestCategory Category => QuestCategory.Social;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 35;
    public int MaxPerDay => 1;
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
