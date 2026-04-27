using System.Collections.Generic;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Periodic board posting: bring Elliott a flower or gem for poetic inspiration.
/// Source: quest table row "Social, Social, Elliott's Poem Inspiration".
internal sealed class ElliottPoemInspiration : IQuestDefinition
{
    public string Id => "Social.ElliottPoem";
    public QuestCategory Category => QuestCategory.Social;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 25;
    public int MaxPerDay => 1;
    public int CooldownDays => 7;

    public bool IsAvailable(QuestContext ctx) =>
        Game1.getCharacterFromName("Elliott") != null &&
        Game1.player.friendshipData.ContainsKey("Elliott");

    public QuestPosting? Build(QuestContext ctx)
    {
        var pool = new List<ResolvedItem>();
        pool.AddRange(ctx.Items.GetItemsByCategory(StardewValley.Object.flowersCategory));
        pool.AddRange(ctx.Items.GetItemsByCategory(StardewValley.Object.GemCategory));

        if (pool.Count == 0)
            return null;

        var pick = pool[Game1.random.Next(pool.Count)];

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Elliott",
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipBasic,
            FriendshipRewardNpc = "Elliott",
            Title = ctx.Helper.Translation.Get("quest.social.elliott.title"),
            Description = ctx.Helper.Translation.Get("quest.social.elliott.description", new { item = pick.DisplayName }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.social.elliott.objective", new { item = pick.DisplayName }),
            TargetMessage = ctx.Helper.Translation.Get("quest.social.elliott.targetMessage")
        };
    }
}
