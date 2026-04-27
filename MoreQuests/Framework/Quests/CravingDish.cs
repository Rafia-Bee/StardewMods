using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: deliver a dish the quest giver loves or likes. Reward is a friendship
/// boost plus the giver gifting back a different loved/liked dish (no gold).
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
        Dictionary<string, string> allRecipes;
        try
        {
            tastes = Game1.content.Load<Dictionary<string, string>>("Data/NPCGiftTastes");
            allRecipes = Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes");
        }
        catch
        {
            return null;
        }

        var knownRecipes = ctx.Items.GetKnownRecipes();
        if (knownRecipes.Count == 0)
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

            var requestableMatches = knownRecipes.Where(r =>
            {
                string bare = StripPrefix(r.OutputItem.QualifiedItemId);
                return loved.Contains(bare) || liked.Contains(bare);
            }).ToList();
            if (requestableMatches.Count == 0)
                continue;

            var dish = requestableMatches[Game1.random.Next(requestableMatches.Count)];
            string requestedBareId = StripPrefix(dish.OutputItem.QualifiedItemId);

            var rewardDish = PickRewardDish(allRecipes, ctx.Items, loved, liked, requestedBareId);

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
                GoldReward = 0,
                FriendshipReward = ctx.Config.FriendshipBasic,
                FriendshipRewardNpc = giver,
                ItemReward = rewardDish?.QualifiedItemId,
                ItemRewardCount = rewardDish != null ? 1 : 0,
                Title = ctx.Helper.Translation.Get("quest.cooking.craving.title", new { npc = giver }),
                Description = ctx.Helper.Translation.Get("quest.cooking.craving.description", new { npc = giver, item = dish.OutputItem.DisplayName }),
                CurrentObjective = ctx.Helper.Translation.Get("quest.cooking.craving.objective", new { item = dish.OutputItem.DisplayName, npc = giver }),
                TargetMessage = ctx.Helper.Translation.Get("quest.cooking.craving.targetMessage", new { item2 = rewardDish?.DisplayName ?? dish.OutputItem.DisplayName })
            };
        }

        return null;
    }

    private static ResolvedItem? PickRewardDish(
        Dictionary<string, string> allRecipes,
        ItemResolver items,
        HashSet<string> loved,
        HashSet<string> liked,
        string excludeBareId)
    {
        var candidates = new List<ResolvedItem>();
        foreach (var (_, raw) in allRecipes)
        {
            var parts = raw.Split('/');
            if (parts.Length < 3)
                continue;
            string outputBare = parts[2].Split(' ')[0];
            if (outputBare == excludeBareId)
                continue;
            if (!loved.Contains(outputBare) && !liked.Contains(outputBare))
                continue;
            var resolved = items.TryResolveItem("(O)" + outputBare);
            if (resolved != null)
                candidates.Add(resolved);
        }
        if (candidates.Count == 0)
            return null;
        return candidates[Game1.random.Next(candidates.Count)];
    }

    private static string StripPrefix(string id) =>
        id.StartsWith("(O)") ? id[3..] : id;
}
