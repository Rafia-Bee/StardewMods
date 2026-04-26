using System;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: deliver X of a seasonal crop (any quality). Tier scales reward + quantity.
/// Source: quest table row "Farming, Crop Delivery, Basic Crop Delivery".
internal sealed class BasicCropDelivery : IQuestDefinition
{
    public string Id => "Farming.BasicCropDelivery";
    public QuestCategory Category => QuestCategory.Farming;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 60;
    public int MaxPerDay => 1;
    public int CooldownDays => 2;

    public bool IsAvailable(QuestContext ctx) => true;

    public QuestPosting? Build(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;

        var crop = crops[Game1.random.Next(crops.Count)];
        int skill = Difficulty.GetSkillLevel(QuestCategory.Farming);
        var tier = Difficulty.TierForSkill(Math.Min(skill, 3));

        int qty = tier switch
        {
            DifficultyTier.Beginner => Game1.random.Next(3, 7),
            DifficultyTier.Intermediate => Game1.random.Next(6, 10),
            _ => Game1.random.Next(8, 12)
        };

        int basePrice = Math.Max(crop.SellPrice, 30);
        int gold = (int)(basePrice * qty * 0.6);
        int floor = ctx.Config.GoldBeginnerBase;
        int cap = ctx.Config.GoldBasicBase;
        gold = Math.Clamp(gold, floor, cap);

        var npc = NpcDispatch.MetHumanNpcs();
        string giver = npc.Count > 0 ? npc[Game1.random.Next(npc.Count)] : "Pierre";

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = tier,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.farming.basic.title", new { npc = giver }),
            Description = ctx.Helper.Translation.Get("quest.farming.basic.description", new { npc = giver, qty, item = crop.DisplayName }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.farming.basic.objective", new { npc = giver, qty, item = crop.DisplayName }),
            TargetMessage = ctx.Helper.Translation.Get("quest.farming.basic.targetMessage")
        };
    }
}
