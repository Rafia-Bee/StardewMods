using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Pipeline;

internal sealed class QuestPipeline
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
            if (_registry.EffectiveSource(def) != TriggerSource.DailyBoard || def.Kind != PostingKind.DailyBoard)
                continue;
            if (!def.IsAvailable(_ctx))
            {
                _ctx.Monitor.Log($"DailyBoard pool: '{def.Id}' unavailable today, skipping.", LogLevel.Debug);
                continue;
            }
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
            {
                _ctx.Monitor.Log($"DailyBoard pool: '{def.Id}' on cooldown ({def.CooldownDays}d), skipping.", LogLevel.Debug);
                continue;
            }
            int w = weights.TryGetValue(def.Id, out int configured) ? configured : def.DefaultWeight;
            if (w <= 0)
            {
                _ctx.Monitor.Log($"DailyBoard pool: '{def.Id}' weight is 0, skipping.", LogLevel.Debug);
                continue;
            }
            pool.Add((def, w));
        }
        _ctx.Monitor.Log($"DailyBoard pool: {pool.Count} eligible definitions after availability/cooldown/weight filters.", LogLevel.Debug);

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
            _antiRepetition.RecordRecency(posting);
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

    // Called once per day after TriggerEvaluator.BeginDay has refreshed snapshots.
    public List<QuestPosting> GenerateTriggered()
    {
        var results = new List<QuestPosting>();
        foreach (var def in _registry.All)
        {
            var src = _registry.EffectiveSource(def);
            if (src == TriggerSource.DailyBoard
                || src == TriggerSource.NpcDialogue
                || src == TriggerSource.SpecialOrder
                || src == TriggerSource.CustomBoard)
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (!_triggers.ShouldFireToday(def.Id, def.OwnerUniqueId, src, def.Trigger, def.CooldownDays))
                continue;

            var posting = def.Build(_ctx);
            if (posting == null)
                continue;
            if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                posting.OwnerUniqueId = def.OwnerUniqueId;
            // Generators sometimes leave Kind unset.
            posting.Kind = def.Kind;
            results.Add(posting);
            _antiRepetition.RecordRecency(posting);
            // Mail / one-shot / dialogue channels don't have a separate accept step,
            // so start the definition cooldown immediately on post.
            _antiRepetition.RecordDefinitionAccepted(posting.DefinitionId);
            if (src == TriggerSource.OneShot)
                _triggers.MarkOneShotFired(def.Id);
        }
        return results;
    }

    public List<QuestPosting> GenerateSpecialOrders()
    {
        var results = new List<QuestPosting>();
        foreach (var def in _registry.All)
        {
            var src = _registry.EffectiveSource(def);
            if (src != TriggerSource.SpecialOrder)
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (!_triggers.ShouldFireToday(def.Id, def.OwnerUniqueId, src, def.Trigger, def.CooldownDays))
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

    // Keyed by OwnerUniqueId/Name. Caller stamps slot lists directly since board-bound
    // quests don't have a delivery channel (they live on the board until accepted).
    public Dictionary<string, List<(QuestPosting posting, BoardDefinition board)>> GenerateCustomBoardPostings(BoardRegistry boardRegistry)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, List<(QuestPosting, BoardDefinition)>>(System.StringComparer.OrdinalIgnoreCase);
        if (boardRegistry == null || boardRegistry.All.Count == 0)
            return result;

        var allDefs = new List<IQuestDefinition>();
        foreach (var def in _registry.All)
        {
            if (_registry.EffectiveSource(def) != TriggerSource.CustomBoard)
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
                _antiRepetition.RecordRecency(posting);
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

    public List<(IQuestDefinition Def, string Npc)> GenerateNpcDialogueQueue()
    {
        var queue = new List<(IQuestDefinition, string)>();
        foreach (var def in _registry.All)
        {
            if (_registry.EffectiveSource(def) != TriggerSource.NpcDialogue)
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

    // Returns true only for the giver=reward-recipient case. Quests that reward a
    // different NPC (e.g. CheckOnGeorge rewards Evelyn) still post.
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
