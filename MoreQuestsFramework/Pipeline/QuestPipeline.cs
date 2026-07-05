using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.State;
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

    // A category with no CategoryEnabled entry, or a true one, is on. Only an explicit false
    // switches it off. An empty category reads as on (quests always carry one). A disabled
    // category is a deliberate off switch like a weight of 0, so it skips silently rather
    // than firing the QuestSkippedToday event.
    private bool IsCategoryEnabled(string? category)
        => string.IsNullOrEmpty(category)
           || !_ctx.Config.CategoryEnabled.TryGetValue(category, out bool enabled)
           || enabled;

    // A notice with no Category rides Social, same default the notice builder uses.
    private static string NoticeCategory(NoticeDef def)
        => string.IsNullOrWhiteSpace(def.Category) ? QuestCategory.Social : def.Category!;

    public List<QuestPosting> GenerateDailyPostings()
    {
        var sw = Stopwatch.StartNew();
        _activePostings.Clear();

        int target = System.Math.Clamp(_ctx.Config.QuestsPerDay, MoreQuestsFrameworkConfig.QuestsPerDayMin, MoreQuestsFrameworkConfig.QuestsPerDayMax);
        var weights = _ctx.Config.QuestWeights;

        var pool = new List<(IQuestDefinition Def, int Weight)>();
        foreach (var def in _registry.All)
        {
            if (_registry.EffectiveSource(def) != TriggerSource.DailyBoard || def.Kind != PostingKind.DailyBoard)
                continue;
            if (!IsCategoryEnabled(def.Category))
            {
                ModEntry.LogDebug($"DailyBoard pool: '{def.Id}' category '{def.Category}' is disabled, skipping.");
                continue;
            }
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
            if (!IsCategoryEnabled(def.Category))
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
            if (!IsCategoryEnabled(def.Category))
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
            if (!IsCategoryEnabled(def.Category))
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
            foreach (var (posting, w) in DrawFromPool(board, group, BoardPoolConfig.Effective(board, _ctx.Config), rng))
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
            int remaining = BoardPoolConfig.Effective(board, _ctx.Config) - draws.Count(d => d.Boards.Contains(board));
            if (remaining <= 0)
                continue;

            var candidates = new List<(QuestPosting Posting, int Weight)>();
            CollectCatchCandidates(board, homePostings, drawByPosting, candidates);
            CollectCatchCandidates(board, homelessPool, drawByPosting, candidates);

            foreach (var posting in SelectByStrategy(board.PoolStrategy, board.Priority, candidates, remaining, rng))
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

    // Draws each board's bulletin notices into its own NoticePoolSize budget, kept separate
    // from the quest draw so notices and quests never compete for slots. Home-board routing
    // only (no catch-all mirroring), each notice appears at most once. Honors Available, the
    // per-notice Weight (scaled by the board's Priority Categories / Types "Notice" map),
    // CooldownDays, the one-shot seen flag, and pinned notices (PinDays / Lifetime), which keep
    // showing without re-rolling until their window ends.
    public List<NoticeDraw> GenerateBoardNotices(
        NoticeRegistry notices, BoardRegistry boards, NoticeStore store, Func<string, int?>? cooldownTierLookup)
    {
        var sw = Stopwatch.StartNew();
        var draws = new List<NoticeDraw>();
        if (notices == null || notices.All.Count == 0 || boards == null || boards.All.Count == 0)
            return draws;

        var (byKey, byOwner) = CustomBoardRouting.BuildBoardMaps(boards);

        var defById = new Dictionary<string, NoticeDef>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in notices.All)
            defById[NoticeRegistry.KeyOf(def)] = def;

        // Carry forward notices still inside their pin window. They show again without being
        // re-rolled, so resolve them to their home board up front and keep them out of the fresh
        // draw below. The call also drops pins whose window has passed.
        var carriedByBoardKey = new Dictionary<string, List<(NoticeDef Def, int ExpiresAfterDay)>>(StringComparer.OrdinalIgnoreCase);
        var carriedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pinned in store.ActivePinned())
        {
            if (!defById.TryGetValue(pinned.DefinitionId, out var def))
                continue;
            if (!IsCategoryEnabled(NoticeCategory(def)))
                continue;
            if (CustomBoardRouting.ResolveHome(def.OwnerUniqueId ?? "", def.CustomBoardId, byKey, byOwner, out string homeKey)
                != CustomBoardRouting.HomeResolution.Home)
                continue;
            if (!carriedByBoardKey.TryGetValue(homeKey, out var carried))
                carriedByBoardKey[homeKey] = carried = new List<(NoticeDef, int)>();
            carried.Add((def, pinned.ExpiresAfterDay));
            carriedIds.Add(pinned.DefinitionId);
        }

        var byBoardKey = new Dictionary<string, List<(NoticeDef Def, int Weight)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in notices.All)
        {
            string id = NoticeRegistry.KeyOf(def);
            if (carriedIds.Contains(id))
                continue;
            if (!IsCategoryEnabled(NoticeCategory(def)))
                continue;
            if (store.IsSeen(id))
                continue;
            if (def.Available != null && def.Available.Count > 0
                && !ConditionEvaluator.Evaluate(def.Available, _ctx.Helper.ModRegistry))
                continue;
            if (store.OnCooldown(id, ResolveNoticeCooldown(def, cooldownTierLookup)))
                continue;
            if (def.Weight <= 0)
                continue;

            switch (CustomBoardRouting.ResolveHome(def.OwnerUniqueId ?? "", def.CustomBoardId, byKey, byOwner, out string homeKey))
            {
                case CustomBoardRouting.HomeResolution.Home:
                    if (!byBoardKey.TryGetValue(homeKey, out var group))
                        byBoardKey[homeKey] = group = new List<(NoticeDef, int)>();
                    group.Add((def, def.Weight));
                    break;
                default:
                    ModEntry.LogDebug($"Notice draw: '{id}' targets board '{def.CustomBoardId ?? "(owner default)"}' which isn't a single home board; dropping.");
                    break;
            }
        }

        var rng = Game1.random;
        int today = Game1.Date.TotalDays;
        foreach (var board in boards.All)
        {
            string boardKey = CustomBoardRouting.KeyOf(board);
            int target = NoticePoolConfig.Effective(board, _ctx.Config);
            if (target <= 0)
                continue;
            int placed = 0;

            // Pinned notices take their slots first. Re-stamp their cooldown each day they show
            // so a cooldown after the pin window counts from the last day they were up.
            if (carriedByBoardKey.TryGetValue(boardKey, out var carried))
            {
                foreach (var (def, expiresAfterDay) in carried)
                {
                    if (placed >= target)
                        break;
                    draws.Add(new NoticeDraw(BuildNotice(def, expiresAfterDay), board));
                    store.RecordPosted(NoticeRegistry.KeyOf(def));
                    placed++;
                }
            }

            if (placed < target && byBoardKey.TryGetValue(boardKey, out var group))
            {
                foreach (var def in SelectNotices(board, group, target - placed, rng))
                {
                    string id = NoticeRegistry.KeyOf(def);
                    int expiry = ComputePinExpiry(def, today);
                    draws.Add(new NoticeDraw(BuildNotice(def, expiry), board));
                    store.RecordPosted(id);
                    if (expiry > today)
                        store.Pin(id, today, expiry);
                    if (def.Once)
                        store.MarkSeen(id);
                    placed++;
                }
            }
        }

        sw.Stop();
        ModEntry.LogDebug($"Generated {draws.Count} notice(s) across {boards.All.Count} board(s) in {sw.Elapsed.TotalMilliseconds:F1} ms.");
        return draws;
    }

    // The TotalDays a notice's pin window ends, or a day before `today` when it isn't pinned.
    // "Week" runs to the end of the current 7-day calendar block (days 1 to 7, 8 to 14, ...);
    // PinDays N runs N days from today.
    private static int ComputePinExpiry(NoticeDef def, int today)
    {
        if (!string.IsNullOrWhiteSpace(def.Lifetime)
            && def.Lifetime.Trim().Equals("Week", StringComparison.OrdinalIgnoreCase))
        {
            int dayOfMonth = Game1.dayOfMonth;
            int daysToWeekEnd = (7 - (dayOfMonth % 7)) % 7;
            return today + daysToWeekEnd;
        }
        if (def.PinDays > 0)
            return today + def.PinDays - 1;
        return today - 1;
    }

    private int ResolveNoticeCooldown(NoticeDef def, Func<string, int?>? cooldownTierLookup)
    {
        if (!string.IsNullOrEmpty(def.CooldownTier) && cooldownTierLookup != null)
        {
            int? resolved = cooldownTierLookup(def.CooldownTier!);
            if (resolved.HasValue)
                return resolved.Value;
        }
        return def.CooldownDays;
    }

    // expiresAfterDay is the pin window's last day (a TotalDays value), or a day before today
    // when the notice isn't pinned. It feeds the {{daysRemaining}} body token.
    private static NoticeInstance BuildNotice(NoticeDef def, int expiresAfterDay) => new()
    {
        DefinitionId = NoticeRegistry.KeyOf(def),
        OwnerUniqueId = def.OwnerUniqueId ?? "",
        Category = string.IsNullOrWhiteSpace(def.Category) ? QuestCategory.Social : def.Category!,
        Title = def.Title ?? "",
        Body = ExpandNoticeBody(def.Body ?? "", expiresAfterDay),
        Giver = def.Giver ?? "",
        Icon = def.Icon ?? "",
        Scale = def.Scale is > 0 ? def.Scale.Value : 0f,
        Image = def.Image ?? "",
        ImageSource = def.ImageSource is { Length: >= 4 }
            ? new Microsoft.Xna.Framework.Rectangle(def.ImageSource[0], def.ImageSource[1], def.ImageSource[2], def.ImageSource[3])
            : null,
    };

    // Replaces the [daysRemaining] token in a pinned notice's body with a short line about how
    // long the pin has left, counting today as the last day. On a notice that isn't pinned the
    // token is just dropped. Resolved per day-start, so it counts down as the window closes.
    // Square brackets, not braces, so it doesn't clash with Content Patcher's own {{ }} tokens.
    private static string ExpandNoticeBody(string body, int expiresAfterDay)
    {
        const string token = "[daysRemaining]";
        if (string.IsNullOrEmpty(body) || !body.Contains(token))
            return body;
        int daysAfterToday = expiresAfterDay - Game1.Date.TotalDays;
        string phrase;
        if (daysAfterToday < 0)
            phrase = "";
        else if (daysAfterToday == 0)
            phrase = Translate("notice.daysRemaining.last", null, "Last day for this notice");
        else if (daysAfterToday == 1)
            phrase = Translate("notice.daysRemaining.one", null, "Pinned for 1 more day");
        else
            phrase = Translate("notice.daysRemaining.many", new { count = daysAfterToday }, $"Pinned for {daysAfterToday} more days");
        return body.Replace(token, phrase);
    }

    private static string Translate(string key, object? tokens, string fallback)
    {
        var t = ModEntry.Translation;
        if (t == null)
            return fallback;
        return (tokens == null ? t.Get(key) : t.Get(key, tokens)).Default(fallback).ToString();
    }

    // Weighted pick without replacement up to `count`, applying the board's Priority to each
    // notice's weight (Categories[category] and Types["Notice"]). FirstAvailable takes them in
    // registration order. No Priority leaves weights as authored, so the draw is unbiased.
    private static List<NoticeDef> SelectNotices(
        BoardDefinition board, List<(NoticeDef Def, int Weight)> group, int count, System.Random rng)
    {
        var result = new List<NoticeDef>();
        if (group.Count == 0 || count <= 0)
            return result;

        if (string.Equals(board.PoolStrategy, "FirstAvailable", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < group.Count && result.Count < count; i++)
                result.Add(group[i].Def);
            return result;
        }

        var pool = new List<(NoticeDef Def, int Weight)>(group.Count);
        foreach (var (def, w) in group)
        {
            int selWeight = ScaleWeight(w, NoticePriorityMultiplier(board.Priority, def.Category));
            if (selWeight > 0)
                pool.Add((def, selWeight));
        }

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
            result.Add(pool[idx].Def);
            pool.RemoveAt(idx);
        }
        return result;
    }

    private static double NoticePriorityMultiplier(BoardPriority? priority, string? category)
    {
        if (priority == null)
            return 1.0;
        double m = 1.0;
        if (priority.Categories != null && category != null && priority.Categories.TryGetValue(category, out double cm))
            m *= cm;
        if (priority.Types != null && priority.Types.TryGetValue("Notice", out double tm))
            m *= tm;
        return m < 0 ? 0 : m;
    }

    private List<(QuestPosting Posting, int Weight)> DrawFromPool(
        BoardDefinition board, List<(IQuestDefinition Def, int Weight)> group, int target, System.Random rng)
    {
        // When the board declares no Priority, weights stay exactly as authored so the draw is
        // unchanged. Only an opted-in board scales them, leaving the original weight intact for
        // downstream catch-all selection.
        bool usePriority = HasPriority(board.Priority);
        var pool = new List<(IQuestDefinition Def, int Weight)>();
        var origWeight = usePriority ? new Dictionary<string, int>() : null;
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
            int selWeight = w;
            if (usePriority)
            {
                double mult = PriorityMultiplier(board.Priority, def.Category, def.OwnerUniqueId);
                selWeight = ScaleWeight(w, mult);
                if (selWeight <= 0)
                    continue;
                origWeight![def.Id] = w;
            }
            pool.Add((def, selWeight));
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

            int keepWeight = usePriority && origWeight!.TryGetValue(def.Id, out int ow) ? ow : w;
            picked.Add((posting, keepWeight));
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
        string? strategy, BoardPriority? priority, List<(QuestPosting Posting, int Weight)> candidates, int count, System.Random rng)
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

        // Apply the catch-all board's own Priority to its pick-up weights. No Priority leaves
        // the original weights, so the draw is unchanged.
        bool usePriority = HasPriority(priority);
        var pool = new List<(QuestPosting Posting, int Weight)>(candidates.Count);
        foreach (var (posting, w) in candidates)
        {
            int selWeight = usePriority
                ? ScaleWeight(w, PriorityMultiplier(priority, posting.Category, posting.OwnerUniqueId))
                : w;
            pool.Add((posting, selWeight));
        }

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

    private static bool HasPriority(BoardPriority? p)
        => p != null && ((p.Categories?.Count ?? 0) > 0 || (p.Owners?.Count ?? 0) > 0);

    // Product of the matching category and owner multipliers (each 1.0 when not listed).
    // A negative result is clamped to 0 (excluded).
    private static double PriorityMultiplier(BoardPriority? priority, string? category, string? owner)
    {
        if (priority == null)
            return 1.0;
        double m = 1.0;
        if (priority.Categories != null && category != null && priority.Categories.TryGetValue(category, out double cm))
            m *= cm;
        if (priority.Owners != null && owner != null && priority.Owners.TryGetValue(owner, out double om))
            m *= om;
        return m < 0 ? 0 : m;
    }

    // Scales a weight by a priority multiplier. Multiplies by 100 first so fractional
    // multipliers (e.g. 0.5) keep resolution as integers; 0 excludes the entry.
    private static int ScaleWeight(int weight, double mult)
    {
        if (mult <= 0)
            return 0;
        return System.Math.Max(1, (int)System.Math.Round(weight * mult * 100.0));
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
            if (!IsCategoryEnabled(def.Category))
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
        return friendship.Points >= maxHearts * Difficulty.FriendshipPointsPerHeart;
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
