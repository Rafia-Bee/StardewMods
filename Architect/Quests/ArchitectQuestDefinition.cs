using System.Collections.Generic;
using Architect.Services;
using MoreQuestsFramework;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace Architect.Quests;

public sealed class ArchitectQuestDefinition : IQuestDefinition, IEligibleGiverSource
{
    public const string QuestId = "RafiaBee.Architect.Redecorate";
    public const string OwnerId = "RafiaBee.Architect";

    private readonly IModHelper _helper;
    private readonly ModConfig _config;

    public ArchitectQuestDefinition(IModHelper helper, ModConfig config)
    {
        _helper = helper;
        _config = config;
    }

    public string Id => QuestId;
    public string OwnerUniqueId => OwnerId;
    public QuestCategory Category => QuestCategory.Social;
    public PostingKind Kind => PostingKind.DailyBoard;
    public int DefaultWeight => _config.QuestWeight;
    public int MaxPerDay => 1;
    public int CooldownDays => _config.CooldownDays;

    public bool IsAvailable(QuestContext ctx)
    {
        return GiverHomeResolver.EligibleGivers().Count > 0;
    }

    // Powers the mq_givers console command.
    public System.Collections.Generic.IReadOnlyList<string> GetEligibleGivers()
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
        int budget = BudgetSizer.Compute(objectives, _config);
        var rewards = GiverRewardBuilder.Build(giver, _config);

        var quest = new ArchitectQuest();
        quest.Initialize(giver, home.Name, objectives, budget);

        string giverDisplay = NpcDisplay.Resolve(giver);

        return new QuestPosting
        {
            DefinitionId = Id,
            OwnerUniqueId = OwnerUniqueId,
            Category = Category,
            Tier = DifficultyTier.Intermediate,
            Kind = PostingKind.DailyBoard,
            QuestType = BoardQuestType.Custom,
            QuestGiver = giver,
            DeadlineDays = _config.DeadlineDays,
            Rewards = rewards,
            Title = _helper.Translation.Get("quest.architect.title", new { npc = giverDisplay }),
            Description = _helper.Translation.Get("quest.architect.description", new { npc = giverDisplay, budget }),
            CurrentObjective = _helper.Translation.Get("quest.architect.objective.summary", new { npc = giverDisplay }),
            TargetMessage = _helper.Translation.Get("quest.architect.targetMessage"),
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

        int maxObjectives = System.Math.Min(_config.MaxObjectives, pool.Count);
        int minObjectives = System.Math.Min(_config.MinObjectives, maxObjectives);
        int objectiveCount = Game1.random.Next(minObjectives, maxObjectives + 1);

        var result = new List<(string, int)>();
        for (int i = 0; i < objectiveCount; i++)
        {
            int count = Game1.random.Next(_config.MinPerObjective, _config.MaxPerObjective + 1);
            result.Add((pool[i], System.Math.Max(1, count)));
        }
        return result;
    }
}
