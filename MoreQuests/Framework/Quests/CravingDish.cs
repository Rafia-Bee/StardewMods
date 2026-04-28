using System.Collections.Generic;
using System.Linq;
using MoreQuests.Framework.Conditions;
using StardewModdingAPI;
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
    public int MaxPerDay => 3;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) => ConditionEvaluator.KnowsAnyCookingRecipe();

    public QuestPosting? Build(QuestContext ctx)
    {
        var npcs = NpcDispatch.MetHumanNpcs();
        if (npcs.Count == 0)
            return null;

        var tastes = ctx.Data.GiftTastes;
        var allRecipes = ctx.Data.CookingRecipes;

        var knownRecipes = ctx.Items.GetKnownRecipes();
        if (knownRecipes.Count == 0)
            return null;

        var candidates = new List<(string Giver, CookingRecipeInfo Dish, HashSet<string> Loved, HashSet<string> Liked, HashSet<string> Neutral)>();
        foreach (var giver in npcs)
        {
            if (!tastes.TryGetValue(giver, out var tasteData))
                continue;

            var fields = tasteData.Split('/');
            // NPCGiftTastes layout (per NPC.GetGiftTasteForThisItem): odd indices hold
            // the item lists, even indices hold the matching dialogue strings.
            //   1=loved, 3=liked, 5=disliked, 7=hated, 9=neutral.
            if (fields.Length < 10)
                continue;

            var loved = fields[1].Split(' ').ToHashSet();
            var liked = fields[3].Split(' ').ToHashSet();
            var neutral = fields[9].Split(' ').ToHashSet();

            foreach (var r in knownRecipes)
            {
                string bare = StripPrefix(r.OutputItem.QualifiedItemId);
                if (loved.Contains(bare) || liked.Contains(bare) || neutral.Contains(bare))
                    candidates.Add((giver, r, loved, liked, neutral));
            }
        }

        if (candidates.Count == 0)
        {
            ctx.Monitor.Log($"CravingDish: no NPC/recipe match across {npcs.Count} met NPCs and {knownRecipes.Count} known recipes.", LogLevel.Trace);
            return null;
        }

        var pick = candidates[Game1.random.Next(candidates.Count)];
        string requestedBareId = StripPrefix(pick.Dish.OutputItem.QualifiedItemId);
        var rewardDish = PickRewardDish(allRecipes, ctx.Items, pick.Loved, pick.Liked, requestedBareId);

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = pick.Giver,
            ObjectiveItemId = pick.Dish.OutputItem.QualifiedItemId,
            ObjectiveItemName = pick.Dish.OutputItem.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = 0,
            FriendshipReward = ctx.Config.FriendshipBasic,
            FriendshipRewardNpc = pick.Giver,
            ItemReward = rewardDish?.QualifiedItemId,
            ItemRewardCount = rewardDish != null ? 1 : 0,
            Title = ctx.Helper.Translation.Get("quest.cooking.craving.title", new { npc = pick.Giver }),
            Description = ctx.Helper.Translation.Get("quest.cooking.craving.description", new { npc = pick.Giver, item = pick.Dish.OutputItem.DisplayName }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.cooking.craving.objective", new { item = pick.Dish.OutputItem.DisplayName, npc = pick.Giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.cooking.craving.targetMessage", new { item2 = rewardDish?.DisplayName ?? pick.Dish.OutputItem.DisplayName })
        };
    }

    private static ResolvedItem? PickRewardDish(
        IReadOnlyDictionary<string, string> allRecipes,
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
