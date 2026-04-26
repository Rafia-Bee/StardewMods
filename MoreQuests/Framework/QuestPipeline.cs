using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// Builds the day's batch of board postings via weighted sampling. Honours per-definition
/// cooldown + max-per-day rules and the pipeline-wide one-per-quest-giver rule.
internal sealed class QuestPipeline
{
    private readonly QuestContext _ctx;
    private readonly AntiRepetition _antiRepetition;
    private readonly List<QuestPosting> _activePostings = new();

    public IReadOnlyList<QuestPosting> ActivePostings => _activePostings;

    public QuestPipeline(QuestContext ctx, AntiRepetition antiRepetition)
    {
        _ctx = ctx;
        _antiRepetition = antiRepetition;
    }

    public List<QuestPosting> GenerateDailyPostings()
    {
        _activePostings.Clear();

        int target = System.Math.Clamp(_ctx.Config.QuestsPerDay, 1, 20);
        var weights = _ctx.Config.QuestWeights;

        var pool = new List<(IQuestDefinition Def, int Weight)>();
        foreach (var def in BoardQuestRegistry.WithKind(PostingKind.DailyBoard))
        {
            if (!def.IsAvailable(_ctx))
                continue;
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                continue;
            int w = weights.TryGetValue(def.Id, out int configured) ? configured : def.DefaultWeight;
            if (w <= 0)
                continue;
            pool.Add((def, w));
        }

        var giversToday = new HashSet<string>();
        var defCounts = new Dictionary<string, int>();
        var rng = Game1.random;
        int safety = 200;

        while (_activePostings.Count < target && pool.Count > 0 && safety-- > 0)
        {
            var (def, _) = WeightedDraw(pool, rng);
            if (def == null)
                break;

            int count = defCounts.TryGetValue(def.Id, out int c) ? c : 0;
            if (count >= def.MaxPerDay)
            {
                pool.RemoveAll(x => x.Def.Id == def.Id);
                continue;
            }

            var posting = def.Build(_ctx);
            if (posting == null)
            {
                pool.RemoveAll(x => x.Def.Id == def.Id);
                continue;
            }

            if (!string.IsNullOrEmpty(posting.QuestGiver) && giversToday.Contains(posting.QuestGiver))
                continue;

            _activePostings.Add(posting);
            _antiRepetition.Record(posting);
            defCounts[def.Id] = count + 1;
            if (!string.IsNullOrEmpty(posting.QuestGiver))
                giversToday.Add(posting.QuestGiver);

            if (defCounts[def.Id] >= def.MaxPerDay)
                pool.RemoveAll(x => x.Def.Id == def.Id);
        }

        _ctx.Monitor.Log($"Generated {_activePostings.Count}/{target} daily-board postings.", LogLevel.Trace);
        return _activePostings;
    }

    /// Mail-delivered triggered quests (e.g. Hay Supply Run on a fixed cadence). Called separately
    /// so triggered quests don't compete with the daily slots.
    public List<QuestPosting> GenerateTriggeredMail()
    {
        var results = new List<QuestPosting>();
        foreach (var def in BoardQuestRegistry.WithKind(PostingKind.Mail))
        {
            if (!def.IsAvailable(_ctx))
                continue;
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                continue;

            var posting = def.Build(_ctx);
            if (posting == null)
                continue;
            results.Add(posting);
            _antiRepetition.Record(posting);
        }
        return results;
    }

    private static (IQuestDefinition? Def, int Weight) WeightedDraw(
        List<(IQuestDefinition Def, int Weight)> pool, System.Random rng)
    {
        int total = pool.Sum(x => x.Weight);
        if (total <= 0)
            return (null, 0);
        int roll = rng.Next(total);
        foreach (var entry in pool)
        {
            roll -= entry.Weight;
            if (roll < 0)
                return entry;
        }
        return pool[^1];
    }
}
