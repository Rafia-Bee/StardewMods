using System.Collections.Generic;
using MoreQuestsFramework;
using MoreQuestsFramework.Rewards;
using StardewValley;

namespace MoreQuests.Quests;

// The "redecorate an NPC's home" daily-board quest. Needs Build Placement Unlocker
// installed, since that's what lets the player place furniture inside a villager's
// house at all. Without it the quest never posts.
public sealed class RedecorateQuestDefinition : IQuestDefinition, IEligibleGiverSource
{
    public const string QuestId = "Social.Redecorate";
    public const string OwnerId = "RafiaBee.MoreQuests";

    // The mod that unlocks furniture placement in NPC homes. The quest is pointless
    // without it, so it only posts when this is loaded.
    public const string BuildPlacementUnlockerId = "PureWinter.BuildPlacementUnlocker";

    public string Id => QuestId;
    public string OwnerUniqueId => OwnerId;
    public string Category => QuestCategory.Social;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => ModEntry.Config.RedecorateQuestWeight;
    public int MaxPerDay => 1;
    public int CooldownDays => ModEntry.Config.RedecorateCooldownDays;

    public bool IsAvailable(QuestContext ctx)
    {
        // The framework gates this by its Social category, so no toggle check here.
        if (!ModEntry.Instance.Helper.ModRegistry.IsLoaded(BuildPlacementUnlockerId))
            return false;
        return GiverHomeResolver.EligibleGivers().Count > 0;
    }

    // Powers the mq_givers console command.
    public IReadOnlyList<string> GetEligibleGivers()
    {
        return GiverHomeResolver.EligibleGivers();
    }

    public QuestPosting? Build(QuestContext ctx)
    {
        var eligible = GiverHomeResolver.EligibleGivers();
        if (eligible.Count == 0)
            return null;

        string giver = eligible[Game1.random.Next(eligible.Count)];
        if (!GiverHomeResolver.TryResolveHome(giver, out var home) || home == null)
            return null;

        var objectives = RollObjectives();
        int budget = BudgetSizer.Compute(objectives, ModEntry.Config);
        var rewards = GiverRewardBuilder.Build(giver, ModEntry.Config);

        var quest = new RedecorateQuest();
        quest.Initialize(giver, home.Name, objectives, budget);

        string giverDisplay = NpcDisplay.Resolve(giver);
        var i18n = ModEntry.I18n;

        return new QuestPosting
        {
            DefinitionId = Id,
            OwnerUniqueId = OwnerUniqueId,
            Category = Category,
            Tier = DifficultyTier.Intermediate,
            Kind = PostingKind.DailyBoard,
            QuestType = BoardQuestType.Custom,
            QuestGiver = giver,
            DeadlineDays = ModEntry.Config.RedecorateDeadlineDays,
            Rewards = rewards,
            Title = i18n.Get("quest.redecorate.title", new { npc = giverDisplay }),
            Description = i18n.Get("quest.redecorate.description", new { npc = giverDisplay, budget }),
            CurrentObjective = i18n.Get("quest.redecorate.objective.summary", new { npc = giverDisplay }),
            TargetMessage = i18n.Get("quest.redecorate.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    private List<(string category, int count)> RollObjectives()
    {
        var pool = new List<string>(FurnitureCategory.All);
        // Shuffle so the categories we pick are distinct and random.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Game1.random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var config = ModEntry.Config;
        int maxObjectives = System.Math.Min(config.RedecorateMaxObjectives, pool.Count);
        int minObjectives = System.Math.Min(config.RedecorateMinObjectives, maxObjectives);
        int objectiveCount = Game1.random.Next(minObjectives, maxObjectives + 1);

        var result = new List<(string, int)>();
        for (int i = 0; i < objectiveCount; i++)
        {
            int count = Game1.random.Next(config.RedecorateMinPerObjective, config.RedecorateMaxPerObjective + 1);
            result.Add((pool[i], System.Math.Max(1, count)));
        }
        return result;
    }
}
