using System;
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

    private Action<string, string, TriggerSource, QuestSkipReason>? _onSkipped;

    public QuestPipeline(QuestContext ctx, QuestRegistry registry, AntiRepetition antiRepetition, TriggerEvaluator triggers)
    {
        _ctx = ctx;
        _registry = registry;
        _antiRepetition = antiRepetition;
        _triggers = triggers;
    }

    public void WireSkipCallback(Action<string, string, TriggerSource, QuestSkipReason>? onSkipped)
        => _onSkipped = onSkipped;

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
                ModEntry.LogDebug($"DailyBoard pool: '{def.Id}' unavailable today, skipping.");
                continue;
            }
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
            {
                ModEntry.LogDebug($"DailyBoard pool: '{def.Id}' on cooldown ({def.CooldownDays}d), skipping.");
                continue;
            }
            int w = weights.TryGetValue(def.Id, out int configured) ? configured : def.DefaultWeight;
            if (w <= 0)
            {
                ModEntry.LogDebug($"DailyBoard pool: '{def.Id}' weight is 0, skipping.");
                continue;
            }
            pool.Add((def, w));
        }
        ModEntry.LogDebug($"DailyBoard pool: {pool.Count} eligible definitions after availability/cooldown/weight filters.");

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
        ModEntry.LogDebug($"Generated {_activePostings.Count}/{target} daily-board postings in {sw.Elapsed.TotalMilliseconds:F1} ms (pool size {initialPoolSize}).");
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
            {
                _onSkipped?.Invoke(def.Id, def.OwnerUniqueId, src, QuestSkipReason.ConditionsNotMet);
                continue;
            }
            // Rolled before ShouldFireToday so a failed roll doesn't burn the trigger's
            // cooldown / yearly date / OneShot flag. BuildingBuilt and MailReceived are
            // skipped because their diff snapshots update unconditionally, so a fail
            // there loses the quest forever instead of retrying.
            if (def.Kind == PostingKind.Mail
                && src != TriggerSource.BuildingBuilt
                && src != TriggerSource.MailReceived)
            {
                int chance = _ctx.Config.MailQuestChancePercent.TryGetValue(def.Id, out int p) ? p : 100;
                chance = System.Math.Clamp(chance, 0, 100);
                if (chance == 0 || (chance < 100 && Game1.random.Next(100) >= chance))
                {
                    _onSkipped?.Invoke(def.Id, def.OwnerUniqueId, src, QuestSkipReason.TriggerNotReady);
                    continue;
                }
            }
            if (!_triggers.ShouldFireToday(def.Id, def.OwnerUniqueId, src, def.Trigger, def.CooldownDays))
            {
                _onSkipped?.Invoke(def.Id, def.OwnerUniqueId, src, QuestSkipReason.TriggerNotReady);
                continue;
            }

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
            {
                _onSkipped?.Invoke(def.Id, def.OwnerUniqueId, src, QuestSkipReason.ConditionsNotMet);
                continue;
            }
            if (!_triggers.ShouldFireToday(def.Id, def.OwnerUniqueId, src, def.Trigger, def.CooldownDays))
            {
                _onSkipped?.Invoke(def.Id, def.OwnerUniqueId, src, QuestSkipReason.TriggerNotReady);
                continue;
            }

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

    // Routes each CustomBoard quest to its home board by id, then lets catch-all boards
    // mirror live postings. Returns one draw per built posting with the boards it appears
    // on; the caller builds the Quest once and shares a single Slot across them.
    public List<CustomBoardDraw> GenerateCustomBoardPostings(BoardRegistry boardRegistry)
    {
        var sw = Stopwatch.StartNew();
        var draws = new List<CustomBoardDraw>();
        if (boardRegistry == null || boardRegistry.All.Count == 0)
            return draws;

        var (boardsByKey, boardsByOwner) = CustomBoardRouting.BuildBoardMaps(boardRegistry);
        var weights = _ctx.Config.QuestWeights;

        var homeDefsByKey = new Dictionary<string, List<(IQuestDefinition Def, int Weight)>>(System.StringComparer.OrdinalIgnoreCase);
        var homelessDefs = new List<(IQuestDefinition Def, int Weight)>();
        foreach (var def in _registry.All)
        {
            if (_registry.EffectiveSource(def) != TriggerSource.CustomBoard)
                continue;
            int w = weights.TryGetValue(def.Id, out int configured) ? configured : def.DefaultWeight;
            switch (CustomBoardRouting.ResolveHome(def, _registry.EffectiveBoard(def), boardsByKey, boardsByOwner, out string homeKey))
            {
                case CustomBoardRouting.HomeResolution.Home:
                    if (!homeDefsByKey.TryGetValue(homeKey, out var group))
                        homeDefsByKey[homeKey] = group = new List<(IQuestDefinition, int)>();
                    group.Add((def, w));
                    break;
                case CustomBoardRouting.HomeResolution.Homeless:
                    homelessDefs.Add((def, w));
                    break;
                default:
                    ModEntry.LogDebug($"Custom-board draw: '{def.Id}' targets unknown board '{_registry.EffectiveBoard(def) ?? def.Trigger?.CustomBoardId}', dropping.");
                    break;
            }
        }

        var rng = Game1.random;
        var drawByPosting = new Dictionary<QuestPosting, CustomBoardDraw>();
        var homePostings = new List<(QuestPosting Posting, int Weight)>();

        foreach (var board in boardRegistry.All)
        {
            if (!homeDefsByKey.TryGetValue(CustomBoardRouting.KeyOf(board), out var group))
                continue;
            foreach (var (posting, w) in DrawFromPool(board, group, System.Math.Max(1, board.PoolSize), rng))
            {
                var draw = new CustomBoardDraw(posting, board);
                draws.Add(draw);
                drawByPosting[posting] = draw;
                homePostings.Add((posting, w));
            }
        }

        var catchAlls = new List<BoardDefinition>();
        foreach (var board in boardRegistry.All)
            if (CustomBoardRouting.IsCatchAll(board))
                catchAlls.Add(board);

        var homelessPool = new List<(QuestPosting Posting, int Weight)>();
        if (catchAlls.Count > 0)
        {
            foreach (var (def, w) in homelessDefs)
            {
                if (w <= 0 || !def.IsAvailable(_ctx) || _antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                    continue;
                if (!AnyCatchAllAccepts(catchAlls, def.OwnerUniqueId))
                    continue;
                var posting = def.Build(_ctx);
                if (posting == null)
                    continue;
                if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                    posting.OwnerUniqueId = def.OwnerUniqueId;
                var draw = new CustomBoardDraw(posting, null);
                draws.Add(draw);
                drawByPosting[posting] = draw;
                homelessPool.Add((posting, w));
            }
        }

        foreach (var board in catchAlls)
        {
            int remaining = System.Math.Max(1, board.PoolSize) - draws.Count(d => d.Boards.Contains(board));
            if (remaining <= 0)
                continue;

            var candidates = new List<(QuestPosting Posting, int Weight)>();
            CollectCatchCandidates(board, homePostings, drawByPosting, candidates);
            CollectCatchCandidates(board, homelessPool, drawByPosting, candidates);

            foreach (var posting in SelectByStrategy(board.PoolStrategy, candidates, remaining, rng))
                drawByPosting[posting].Boards.Add(board);
        }

        var survivors = new List<CustomBoardDraw>(draws.Count);
        foreach (var draw in draws)
        {
            if (draw.Boards.Count == 0)
                continue;
            _antiRepetition.RecordRecency(draw.Posting);
            survivors.Add(draw);
        }

        sw.Stop();
        ModEntry.LogDebug($"Generated {survivors.Count} custom-board draw(s) across {boardRegistry.All.Count} board(s) in {sw.Elapsed.TotalMilliseconds:F1} ms.");
        return survivors;
    }

    private List<(QuestPosting Posting, int Weight)> DrawFromPool(
        BoardDefinition board, List<(IQuestDefinition Def, int Weight)> group, int target, System.Random rng)
    {
        var pool = new List<(IQuestDefinition Def, int Weight)>();
        foreach (var (def, w) in group)
        {
            if (!CategoryAllowedOnBoard(board, def.Category))
                continue;
            if (!def.IsAvailable(_ctx))
                continue;
            if (_antiRepetition.DefinitionOnCooldown(def.Id, def.CooldownDays))
                continue;
            if (w <= 0)
                continue;
            pool.Add((def, w));
        }

        var picked = new List<(QuestPosting, int)>();
        var defCounts = new Dictionary<string, int>();
        int safety = 200;
        while (picked.Count < target && pool.Count > 0 && safety-- > 0)
        {
            var (def, w) = WeightedDraw(pool, rng);
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

            picked.Add((posting, w));
            defCounts[def.Id] = count + 1;
            if (defCounts[def.Id] >= def.MaxPerDay)
                pool.RemoveAll(x => x.Def.Id == def.Id);
        }
        return picked;
    }

    private static bool AnyCatchAllAccepts(List<BoardDefinition> catchAlls, string ownerUniqueId)
    {
        foreach (var board in catchAlls)
            if (CustomBoardRouting.Catches(board, ownerUniqueId))
                return true;
        return false;
    }

    private static void CollectCatchCandidates(
        BoardDefinition board,
        List<(QuestPosting Posting, int Weight)> source,
        Dictionary<QuestPosting, CustomBoardDraw> drawByPosting,
        List<(QuestPosting Posting, int Weight)> sink)
    {
        foreach (var (posting, w) in source)
        {
            if (drawByPosting[posting].Boards.Contains(board))
                continue;
            if (!CustomBoardRouting.Catches(board, posting.OwnerUniqueId))
                continue;
            if (!CategoryAllowedOnBoard(board, posting.Category))
                continue;
            sink.Add((posting, w));
        }
    }

    private static List<QuestPosting> SelectByStrategy(
        string? strategy, List<(QuestPosting Posting, int Weight)> candidates, int count, System.Random rng)
    {
        var result = new List<QuestPosting>();
        if (candidates.Count == 0 || count <= 0)
            return result;

        if (string.Equals(strategy, "FirstAvailable", System.StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < candidates.Count && result.Count < count; i++)
                result.Add(candidates[i].Posting);
            return result;
        }

        var pool = new List<(QuestPosting Posting, int Weight)>(candidates);
        while (result.Count < count && pool.Count > 0)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
                total += System.Math.Max(0, pool[i].Weight);
            if (total <= 0)
                break;
            int roll = rng.Next(total);
            int idx = 0;
            for (; idx < pool.Count; idx++)
            {
                roll -= System.Math.Max(0, pool[idx].Weight);
                if (roll < 0)
                    break;
            }
            if (idx >= pool.Count)
                idx = pool.Count - 1;
            result.Add(pool[idx].Posting);
            pool.RemoveAt(idx);
        }
        return result;
    }

    private static bool CategoryAllowedOnBoard(BoardDefinition board, string category)
    {
        if (board.AllowedCategories == null || board.AllowedCategories.Count == 0)
            return true;
        for (int i = 0; i < board.AllowedCategories.Count; i++)
        {
            if (string.Equals(board.AllowedCategories[i], category, System.StringComparison.OrdinalIgnoreCase))
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
