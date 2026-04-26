using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: deliver a dish the quest giver loves or likes.
/// Source: quest table row "Cooking, Craving a meal, Craving <dish>".
internal sealed class CravingDish : IQuestDefinition
{
    public string Id => "Cooking.CravingDish";
    public QuestCategory Category => QuestCategory.Cooking;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 30;
    public int MaxPerDay => 1;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) => Game1.player.cookingRecipes.Length > 0;

    public QuestPosting? Build(QuestContext ctx)
    {
        var npcs = NpcDispatch.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;

        Dictionary<string, string> tastes;
        try
        {
            tastes = Game1.content.Load<Dictionary<string, string>>("Data/NPCGiftTastes");
        }
        catch
        {
            return null;
        }

        var recipes = ctx.Items.GetKnownRecipes();
        if (recipes.Count == 0)
            return null;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            string giver = npcs[Game1.random.Next(npcs.Count)];
            if (!tastes.TryGetValue(giver, out var tasteData))
                continue;

            var fields = tasteData.Split('/');
            if (fields.Length < 9)
                continue;

            var loved = fields[0].Split(' ').ToHashSet();
            var liked = fields[2].Split(' ').ToHashSet();

            var matches = recipes.Where(r =>
            {
                string bare = StripPrefix(r.OutputItem.QualifiedItemId);
                return loved.Contains(bare) || liked.Contains(bare);
            }).ToList();

            if (matches.Count == 0)
                continue;

            var dish = matches[Game1.random.Next(matches.Count)];
            return new QuestPosting
            {
                DefinitionId = Id,
                Category = Category,
                Tier = DifficultyTier.Intermediate,
                QuestType = BoardQuestType.ItemDelivery,
                QuestGiver = giver,
                ObjectiveItemId = dish.OutputItem.QualifiedItemId,
                ObjectiveItemName = dish.OutputItem.DisplayName,
                ObjectiveQuantity = 1,
                DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
                GoldReward = ctx.Config.GoldBasicBase / 2,
                FriendshipReward = ctx.Config.FriendshipBasic,
                FriendshipRewardNpc = giver,
                Title = ctx.Helper.Translation.Get("quest.cooking.craving.title", new { npc = giver }),
                Description = ctx.Helper.Translation.Get("quest.cooking.craving.description", new { npc = giver, item = dish.OutputItem.DisplayName }),
                CurrentObjective = ctx.Helper.Translation.Get("quest.cooking.craving.objective", new { item = dish.OutputItem.DisplayName, npc = giver }),
                TargetMessage = ctx.Helper.Translation.Get("quest.cooking.craving.targetMessage")
            };
        }

        return null;
    }

    private static string StripPrefix(string id) =>
        id.StartsWith("(O)") ? id[3..] : id;
}
