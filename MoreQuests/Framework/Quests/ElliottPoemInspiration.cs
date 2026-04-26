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

    private static readonly (string Id, string Name)[] Pool =
    {
        ("(O)591", "Tulip"),
        ("(O)597", "Blue Jazz"),
        ("(O)593", "Summer Spangle"),
        ("(O)376", "Poppy"),
        ("(O)421", "Sunflower"),
        ("(O)418", "Crocus"),
        ("(O)60", "Emerald"),
        ("(O)62", "Aquamarine"),
        ("(O)64", "Ruby"),
        ("(O)66", "Amethyst"),
        ("(O)70", "Jade"),
        ("(O)72", "Diamond")
        // TODO: make this dynamic for modded gems/flowers.
    };

    public bool IsAvailable(QuestContext ctx) =>
        Game1.getCharacterFromName("Elliott") != null &&
        Game1.player.friendshipData.ContainsKey("Elliott");

    public QuestPosting? Build(QuestContext ctx)
    {
        var pick = Pool[Game1.random.Next(Pool.Length)];

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Elliott",
            ObjectiveItemId = pick.Id,
            ObjectiveItemName = pick.Name,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipBasic,
            FriendshipRewardNpc = "Elliott",
            Title = ctx.Helper.Translation.Get("quest.social.elliott.title"),
            Description = ctx.Helper.Translation.Get("quest.social.elliott.description", new { item = pick.Name }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.social.elliott.objective", new { item = pick.Name }),
            TargetMessage = ctx.Helper.Translation.Get("quest.social.elliott.targetMessage")
        };
    }
}
