using System.Collections.Generic;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: gather and ship X seasonal forage items.
/// Source: quest table row "Foraging, Basic Gather, Seasonal Foraging".
internal sealed class SeasonalForaging : IQuestDefinition
{
    public string Id => "Foraging.Seasonal";
    public QuestCategory Category => QuestCategory.Foraging;
    public PostingKind Kind => PostingKind.DailyBoard;

    private static readonly Dictionary<string, (string Id, string Name)[]> SeasonalForage = new()
    {
        ["spring"] = new[] { ("(O)16", "Wild Horseradish"), ("(O)18", "Daffodil"), ("(O)20", "Leek"), ("(O)22", "Dandelion") },
        ["summer"] = new[] { ("(O)396", "Spice Berry"), ("(O)398", "Grape"), ("(O)402", "Sweet Pea"), ("(O)257", "Morel") },
        ["fall"] = new[] { ("(O)404", "Common Mushroom"), ("(O)406", "Wild Plum"), ("(O)408", "Hazelnut"), ("(O)410", "Blackberry") },
        ["winter"] = new[] { ("(O)412", "Winter Root"), ("(O)414", "Crystal Fruit"), ("(O)416", "Snow Yam"), ("(O)418", "Crocus") }
    };

    public bool IsAvailable(QuestContext ctx) => SeasonalForage.ContainsKey(ctx.Season);

    public QuestPosting? Build(QuestContext ctx)
    {
        if (!SeasonalForage.TryGetValue(ctx.Season, out var pool))
            return null;

        var pick = pool[Game1.random.Next(pool.Length)];
        int qty = Game1.random.Next(3, 8);
        int gold = ctx.Config.GoldBeginnerBase;

        var npcs = NpcDispatch.MetHumanNpcs();
        string giver = npcs.Count > 0 ? npcs[Game1.random.Next(npcs.Count)] : "Linus";

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ResourceCollection,
            QuestGiver = giver,
            ObjectiveItemId = pick.Id,
            ObjectiveItemName = pick.Name,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.foraging.seasonal.title", new { npc = giver }),
            Description = ctx.Helper.Translation.Get("quest.foraging.seasonal.description", new { npc = giver, qty, item = pick.Name }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.foraging.seasonal.objective", new { qty, item = pick.Name, npc = giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.foraging.seasonal.targetMessage")
        };
    }
}
