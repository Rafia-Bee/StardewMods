using MoreQuestsFramework;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;

namespace ExampleCSharpIQuestDef;

/// A single quest implemented as a pure C# `IQuestDefinition`. Same shape as the
/// framework's built-in `Vanilla.ItemDelivery`, just with our own fields.
///
/// `IsAvailable` is the cheap pre-check that runs before the framework spends
/// any time generating the posting. `Build` does the actual work. Prefer `ctx`
/// (`ctx.Season`, `ctx.DayOfMonth`, `ctx.Year`, `ctx.Helper`, `ctx.Data`, etc.)
/// over reading `Game1.*` so the same check can be dry-run via
/// `IMoreQuestsApi.IsQuestAvailable`.
public sealed class AppleForLewisDefinition : IQuestDefinition
{
    private readonly IModHelper _helper;

    public AppleForLewisDefinition(IModHelper helper)
    {
        _helper = helper;
    }

    public string Id => "ExampleCSharpIQuestDef.AppleForLewis";
    public string Category => QuestCategory.Foraging;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 20;
    public int MaxPerDay => 1;
    public int CooldownDays => 14;

    public bool IsAvailable(QuestContext ctx)
    {
        return ctx.Season == "fall";
    }

    public QuestPosting? Build(QuestContext ctx)
    {
        const string giver = "Lewis";

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = "(O)613",
            ObjectiveItemName = "Apple",
            ObjectiveQuantity = 1,
            DeadlineDays = ctx.Config.DeadlineShort,
            Rewards =
            {
                new MoneyReward(500),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            },
            Title = _helper.Translation.Get("example.apple.title"),
            Description = _helper.Translation.Get("example.apple.description"),
            CurrentObjective = _helper.Translation.Get("example.apple.objective"),
            TargetMessage = _helper.Translation.Get("example.apple.targetMessage")
        };
    }
}
