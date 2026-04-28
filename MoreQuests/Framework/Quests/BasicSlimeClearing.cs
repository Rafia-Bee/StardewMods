using MoreQuests.Framework.Conditions;
using StardewValley;

namespace MoreQuests.Framework.Quests;

/// Daily board: slay X slimes (any variant) in the mines. Quest giver is picked from the
/// CombatVendor dispatch role. Vanilla Marlon is excluded because right-clicking him
/// opens the Adventure Guild shop instead of triggering dialogue, so OnNpcSocialized
/// would never fire and the quest could never be turned in.
/// Source: quest table row "Mining, Combat, Basic Slime Clearing".
internal sealed class BasicSlimeClearing : IQuestDefinition
{
    public string Id => "Mining.BasicSlimeClearing";
    public QuestCategory Category => QuestCategory.Mining;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => 50;
    public int MaxPerDay => 1;
    public int CooldownDays => 7;

    public bool IsAvailable(QuestContext ctx) => ConditionEvaluator.MinDeepestMineLevel(1);

    public QuestPosting? Build(QuestContext ctx)
    {
        string? giver = NpcDispatch.Pick(ctx.Helper.ModRegistry, NpcDispatch.Role.CombatVendor);
        if (giver == null)
            return null;

        int qty = Game1.random.Next(8, 16);
        int gold = ctx.Config.GoldBeginnerBase + Game1.random.Next(0, 100);

        var quest = new AnySlimeQuest
        {
            target = { Value = giver },
            monsterName = { Value = "Green Slime" },
            numberToKill = { Value = qty },
            reward = { Value = gold },
            targetMessage = ctx.Helper.Translation.Get("quest.mining.slime.targetMessage", new { npc = giver })
        };

        return new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = giver,
            ObjectiveItemId = "Green Slime",
            ObjectiveItemName = "Green Slime",
            ObjectiveQuantity = qty,
            TargetMonster = "Green Slime",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            GoldReward = gold,
            Title = ctx.Helper.Translation.Get("quest.mining.slime.title"),
            Description = ctx.Helper.Translation.Get("quest.mining.slime.description", new { qty, npc = giver }),
            CurrentObjective = ctx.Helper.Translation.Get("quest.mining.slime.objective", new { qty, npc = giver }),
            TargetMessage = ctx.Helper.Translation.Get("quest.mining.slime.targetMessage", new { npc = giver }),
            PreBuiltQuest = quest
        };
    }
}
