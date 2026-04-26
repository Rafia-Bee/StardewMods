using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: Evelyn asks the farmer to talk to George and report back.
/// Vanilla SocializeQuest behavior. Modeled here as a simple item-less posting.
/// The underlying social-tracking subclass will be implemented in a later phase.
/// Source: quest table row "Social, Check-in, Check on George".
internal sealed class CheckOnGeorge : IQuestDefinition
{
    public string Id => "Social.CheckOnGeorge";
    public QuestCategory Category => QuestCategory.Social;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 25;
    public int MaxPerDay => 1;
    public int CooldownDays => 21;

    public bool IsAvailable(QuestContext ctx) =>
        Game1.getCharacterFromName("George") != null &&
        Game1.getCharacterFromName("Evelyn") != null;

    public QuestPosting? Build(QuestContext ctx)
    {
        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Evelyn",
            ObjectiveItemId = "(O)216",
            ObjectiveItemName = "Bread",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipMid,
            FriendshipRewardNpc = "Evelyn",
            Title = ctx.Helper.Translation.Get("quest.social.george.title"),
            Description = ctx.Helper.Translation.Get("quest.social.george.description"),
            CurrentObjective = ctx.Helper.Translation.Get("quest.social.george.objective"),
            TargetMessage = ctx.Helper.Translation.Get("quest.social.george.targetMessage")
        };
    }
}
