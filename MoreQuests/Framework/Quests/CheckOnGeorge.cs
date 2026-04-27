using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: Evelyn asks the farmer to bring George a liked or loved gift,
/// chat with him, then report back. Player keeps any non-consumed items.
/// Friendship reward goes to both Evelyn and George. The custom
/// `CheckOnGeorgeQuest` handles the gift detection and reporting flow.
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
        var quest = new CheckOnGeorgeQuest
        {
            giftRecipient = { Value = "George" },
            reportTo = { Value = "Evelyn" }
        };

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Socialize,
            QuestGiver = "Evelyn",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipMid,
            FriendshipRewardNpc = "Evelyn",
            Consequences =
            {
                new QuestConsequence { NpcName = "George", FriendshipChange = ctx.Config.FriendshipMid, Tier = ConsequenceTier.Positive }
            },
            Title = ctx.Helper.Translation.Get("quest.social.george.title"),
            Description = ctx.Helper.Translation.Get("quest.social.george.description"),
            CurrentObjective = ctx.Helper.Translation.Get("quest.social.george.objective"),
            TargetMessage = ctx.Helper.Translation.Get("quest.social.george.targetMessage"),
            PreBuiltQuest = quest
        };
    }
}
