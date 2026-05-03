using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Pipeline;

/// Builds the day's batch of board postings via weighted sampling. Honours per-definition
/// cooldown + max-per-day rules and the pipeline-wide one-per-quest-giver rule.
public sealed class QuestPipeline
{
    private readonly QuestContext _ctx;
    private readonly QuestRegistry _registry;
    private readonly AntiRepetition _antiRepetition;
    private readonly TriggerEvaluator _triggers;
    private readonly List<QuestPosting> _activePostings = new();

    public IReadOnlyList<QuestPosting> ActivePostings => _activePostings;

    public QuestPipeline(QuestContext ctx, QuestRegistry registry, AntiRepetition antiRepetition, TriggerEvaluator triggers)
    {
        _ctx = ctx;
        _registry = registry;
        _antiRepetition = antiRepetition;
        _triggers = triggers;
    }

    public List<QuestPosting> GenerateDailyPostings()
    {
        var sw = Stopwatch.StartNew();
        _activePostings.Clear();

        int target = System.Math.Clamp(_ctx.Config.QuestsPerDay, 1, 20);
        var weights = _ctx.Config.QuestWeights;

        var pool = new List<(IQuestDefinition Def, int Weight)>();
        foreach (var def in _registry.All)
        {
            if (def.Source != TriggerSource.DailyBoard || def.Kind != PostingKind.DailyBoard)
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                continue;
            int w = weights.TryGetValue(def.Id, out int configured) ? configured : def.DefaultWeight;
            if (w <= 0)
                continue;
            pool.Add((def, w));
        }

        int initialPoolSize = pool.Count;
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
            if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                posting.OwnerUniqueId = def.OwnerUniqueId;

            if (!_ctx.Config.AllowDuplicateGiverPerDay
                && !string.IsNullOrEmpty(posting.QuestGiver)
                && giversToday.Contains(posting.QuestGiver))
                continue;

            if (_ctx.Config.SkipFriendshipQuestsAtMaxHeart && IsAtMaxHeartWithRewardNpc(posting))
                continue;

            _activePostings.Add(posting);
            _antiRepetition.Record(posting);
            defCounts[def.Id] = count + 1;
            if (!string.IsNullOrEmpty(posting.QuestGiver))
                giversToday.Add(posting.QuestGiver);

            if (defCounts[def.Id] >= def.MaxPerDay)
                pool.RemoveAll(x => x.Def.Id == def.Id);
        }

        sw.Stop();
        _ctx.Monitor.Log(
            $"Generated {_activePostings.Count}/{target} daily-board postings in {sw.Elapsed.TotalMilliseconds:F1} ms (pool size {initialPoolSize}).",
            LogLevel.Trace);
        return _activePostings;
    }

    /// Triggered (non-daily-board) quests delivered via mail or NPC-dialogue. Drives the
    /// Phase 6 trigger sources: Periodic, DateLocked, DateRange, OneShot, BuildingBuilt,
    /// MailReceived, WeatherForecast, plus the legacy `Mail` cooldown source. Called once
    /// per day after the building/mail snapshots have been refreshed in `TriggerEvaluator.BeginDay`.
    public List<QuestPosting> GenerateTriggered()
    {
        var results = new List<QuestPosting>();
        foreach (var def in _registry.All)
        {
            if (def.Source == TriggerSource.DailyBoard
                || def.Source == TriggerSource.NpcDialogue
                || def.Source == TriggerSource.SpecialOrder
                || def.Source == TriggerSource.CustomBoard)
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (!_triggers.ShouldFireToday(def.Id, def.Source, def.Trigger, def.CooldownDays))
                continue;

            var posting = def.Build(_ctx);
            if (posting == null)
                continue;
            if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                posting.OwnerUniqueId = def.OwnerUniqueId;
            // Force the posting's delivery channel to match what the definition declared,
            // since generators sometimes leave Kind unset.
            posting.Kind = def.Kind;
            results.Add(posting);
            _antiRepetition.Record(posting);
        }
        return results;
    }

    /// SpecialOrder-source definitions whose `StartDate` matches today and whose cooldown
    /// has elapsed. The framework writes each into `Data/SpecialOrders` via
    /// `SpecialOrderWriter`; vanilla owns the accept/track/reward path from there.
    public List<QuestPosting> GenerateSpecialOrders()
    {
        var results = new List<QuestPosting>();
        foreach (var def in _registry.All)
        {
            if (def.Source != TriggerSource.SpecialOrder)
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (!_triggers.ShouldFireToday(def.Id, def.Source, def.Trigger, def.CooldownDays))
                continue;

            var posting = def.Build(_ctx);
            if (posting == null)
                continue;
            if (posting.SpecialOrder == null)
            {
                _ctx.Monitor.Log(
                    $"SpecialOrder definition '{def.Id}' returned a posting with no SpecialOrder spec; skipping.",
                    LogLevel.Warn);
                continue;
            }
            if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                posting.OwnerUniqueId = def.OwnerUniqueId;
            posting.Kind = PostingKind.SpecialOrder;
            results.Add(posting);
        }
        return results;
    }

    /// Per-board batch of `TriggerSource.CustomBoard` postings, keyed by the board's
    /// `OwnerUniqueId/Name` lookup key. Each board draws independently — its own
    /// `AllowedCategories` filter applies, its own `PoolSize` caps the result, and the
    /// pipeline's daily-board cooldown / availability gates still apply.
    ///
    /// `GenerateCustomBoardPostings` is called from `ModEntry.OnDayStarted` after the
    /// help-wanted batch lands. Postings are not posted via `QuestPoster` (board-bound
    /// quests don't have a delivery channel of their own — they live on the board until
    /// the player opens it and clicks Accept), so the caller stamps slot lists directly.
    public Dictionary<string, List<(QuestPosting posting, BoardDefinition board)>> GenerateCustomBoardPostings(BoardRegistry boardRegistry)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, List<(QuestPosting, BoardDefinition)>>(System.StringComparer.OrdinalIgnoreCase);
        if (boardRegistry == null || boardRegistry.All.Count == 0)
            return result;

        // Gather every CustomBoard-source definition once so the per-board passes only
        // re-evaluate availability + cooldown filters.
        var allDefs = new List<IQuestDefinition>();
        foreach (var def in _registry.All)
        {
            if (def.Source != TriggerSource.CustomBoard)
                continue;
            allDefs.Add(def);
        }
        if (allDefs.Count == 0)
            return result;

        var weights = _ctx.Config.QuestWeights;
        var rng = Game1.random;

        foreach (var board in boardRegistry.All)
        {
            string boardKey = (board.OwnerUniqueId ?? string.Empty) + "/" + (board.Name ?? string.Empty);
            int target = System.Math.Max(1, board.PoolSize);

            var pool = new List<(IQuestDefinition Def, int Weight)>();
            foreach (var def in allDefs)
            {
                if (!CategoryAllowedOnBoard(board, def))
                    continue;
                if (!def.IsAvailable(_ctx))
                    continue;
                if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                    continue;
                int w = weights.TryGetValue(def.Id, out int configured) ? configured : def.DefaultWeight;
                if (w <= 0)
                    continue;
                pool.Add((def, w));
            }

            var picked = new List<(QuestPosting, BoardDefinition)>();
            var defCounts = new Dictionary<string, int>();
            int safety = 200;
            while (picked.Count < target && pool.Count > 0 && safety-- > 0)
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
                if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                    posting.OwnerUniqueId = def.OwnerUniqueId;

                picked.Add((posting, board));
                _antiRepetition.Record(posting);
                defCounts[def.Id] = count + 1;
                if (defCounts[def.Id] >= def.MaxPerDay)
                    pool.RemoveAll(x => x.Def.Id == def.Id);
            }

            result[boardKey] = picked;
        }

        sw.Stop();
        int total = result.Sum(kv => kv.Value.Count);
        _ctx.Monitor.Log(
            $"Generated {total} custom-board posting(s) across {result.Count} board(s) in {sw.Elapsed.TotalMilliseconds:F1} ms.",
            LogLevel.Trace);
        return result;
    }

    private static bool CategoryAllowedOnBoard(BoardDefinition board, IQuestDefinition def)
    {
        if (board.AllowedCategories == null || board.AllowedCategories.Count == 0)
            return true;
        string defCategory = def.Category.ToString();
        for (int i = 0; i < board.AllowedCategories.Count; i++)
        {
            if (string.Equals(board.AllowedCategories[i], defCategory, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// Returns every NpcDialogue-source definition that should fire today (per
    /// IsAvailable + cooldown). The caller queues them through the dialogue watcher
    /// so they post next time the player speaks with the target NPC.
    public List<(IQuestDefinition Def, string Npc)> GenerateNpcDialogueQueue()
    {
        var queue = new List<(IQuestDefinition, string)>();
        foreach (var def in _registry.All)
        {
            if (def.Source != TriggerSource.NpcDialogue)
                continue;
            if (string.IsNullOrEmpty(def.Trigger.Npc))
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                continue;
            queue.Add((def, def.Trigger.Npc!));
        }
        return queue;
    }

    /// True if the posting rewards friendship to its own quest giver and that NPC is
    /// already at max heart level. Quests that reward a different NPC than the giver
    /// (e.g. CheckOnGeorge rewards Evelyn for caring about George) still post.
    private static bool IsAtMaxHeartWithRewardNpc(QuestPosting posting)
    {
        var giverReward = posting.GiverFriendshipReward;
        if (giverReward == null || giverReward.Points <= 0)
            return false;

        var npc = Game1.getCharacterFromName(giverReward.Npc);
        if (npc == null)
            return false;
        if (!Game1.player.friendshipData.TryGetValue(giverReward.Npc, out var friendship))
            return false;

        int maxHearts = Utility.GetMaximumHeartsForCharacter(npc);
        return friendship.Points >= maxHearts * 250;
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
