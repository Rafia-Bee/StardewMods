using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// Builds the day's batch of board postings by sampling daily-board definitions.
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

        int count = _ctx.Config.QuestsPerDay;
        var rng = Game1.random;

        var dailyPool = BoardQuestRegistry
            .WithKind(PostingKind.DailyBoard)
            .Where(d => d.IsAvailable(_ctx))
            .Where(d => !_antiRepetition.DefinitionRecent(d.Id))
            .ToList();

        if (dailyPool.Count < count)
        {
            dailyPool = BoardQuestRegistry
                .WithKind(PostingKind.DailyBoard)
                .Where(d => d.IsAvailable(_ctx))
                .ToList();
        }

        var picked = new HashSet<QuestCategory>();
        while (_activePostings.Count < count && dailyPool.Count > 0)
        {
            int idx = rng.Next(dailyPool.Count);
            var def = dailyPool[idx];
            dailyPool.RemoveAt(idx);

            if (picked.Contains(def.Category))
                continue;

            var posting = def.Build(_ctx);
            if (posting == null)
                continue;

            _activePostings.Add(posting);
            _antiRepetition.Record(posting);
            picked.Add(def.Category);
        }

        _ctx.Monitor.Log($"Generated {_activePostings.Count} daily-board postings.", LogLevel.Trace);
        return _activePostings;
    }

    /// Mail-delivered triggered quests (e.g. Hay Supply Run on a fixed cadence). Called separately
    /// from the daily board pass so triggered quests don't compete with the daily slots.
    public List<QuestPosting> GenerateTriggeredMail()
    {
        var results = new List<QuestPosting>();
        foreach (var def in BoardQuestRegistry.WithKind(PostingKind.Mail))
        {
            if (!def.IsAvailable(_ctx))
                continue;
            if (_antiRepetition.DefinitionRecent(def.Id))
                continue;

            var posting = def.Build(_ctx);
            if (posting == null)
                continue;
            results.Add(posting);
            _antiRepetition.Record(posting);
        }
        return results;
    }
}
