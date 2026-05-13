using System;
using System.Collections.Generic;
using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.State;
using StardewModdingAPI;

namespace MoreQuestsFramework.Consequences;

/// Routes every `ConsequenceSpec` to the matching `IConsequenceHandler`. One singleton
/// per save load, wired in `ModEntry.OnSaveLoaded` after the state store and gift-tastes
/// scanner are ready. Handler registration is open from `RegistrationOpen` until the
/// registration window closes; built-in handlers seed first so consumer mods can replace
/// them by name.
public sealed class ConsequenceEngine
{
    private readonly Dictionary<ConsequenceTier, IConsequenceHandler> _handlers = new();
    private readonly MoreQuestsFrameworkConfig _config;
    private readonly GameDataCache _cache;
    private readonly GiftTastesScanner _scanner;
    private readonly FrameworkState _state;
    private readonly IMonitor _monitor;

    /// Set by `ModEntry.OnSaveLoaded` to the live engine instance. Static so quest
    /// subclasses (`MoreQuestsItemDeliveryQuest`, `AdventureQuest`, ...) can fire the
    /// engine from their `questComplete()` overrides without threading an instance
    /// reference through every subclass. Null when no save is loaded, the helper
    /// no-ops gracefully so unit-test / authoring scenarios work without one.
    public static ConsequenceEngine? Active { get; set; }

    public ConsequenceEngine(
        MoreQuestsFrameworkConfig config,
        GameDataCache cache,
        FrameworkState state,
        IMonitor monitor)
    {
        _config = config;
        _cache = cache;
        _scanner = new GiftTastesScanner(cache);
        _state = state;
        _monitor = monitor;

        // Built-ins seed first; consumer-mod overrides land via Register() during
        // RegistrationOpen and replace the entry by tier.
        Register(ConsequenceTier.Tier0, new Tier0Handler());
        Register(ConsequenceTier.Tier1, new Tier1Handler());
        Register(ConsequenceTier.Tier2, new Tier2Handler());
        Register(ConsequenceTier.Tier3, new Tier3Handler());
        Register(ConsequenceTier.Special, new SpecialTierHandler());
    }

    public GiftTastesScanner GiftTastes => _scanner;
    public FrameworkState State => _state;

    public void Register(ConsequenceTier tier, IConsequenceHandler handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        _handlers[tier] = handler;
    }

    /// Drop pending dialogue entries the player has ducked for too long. Fired from
    /// `ModEntry.OnDayStarted`. Anything whose `EarliestFireDay` is more than
    /// `ConsequenceGraceDays` days in the past stops being a plausible reaction,
    /// an NPC isn't going to bring up an overfishing complaint a year after the fact.
    /// Stale-drop count is logged at Trace.
    public void SweepExpired()
    {
        int today = StardewValley.Game1.Date?.TotalDays ?? 0;
        int grace = _config.ConsequenceGraceDays > 0 ? _config.ConsequenceGraceDays : 7;
        var queue = _state.PendingConsequenceLines;
        int dropped = 0;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].EarliestFireDay + grace < today)
            {
                queue.RemoveAt(i);
                dropped++;
            }
        }
        if (dropped > 0)
            _monitor.Log(
                $"ConsequenceEngine: swept {dropped} stale pending dialogue entries (older than {grace} days past their fire day).",
                LogLevel.Trace);
    }

    /// Fire a consequence on quest completion. Resolves the loved/hated NPC sets via
    /// `Source` + `Subject`, builds the context, and forwards to the registered handler.
    public void Apply(ConsequenceSpec? spec)
    {
        if (spec == null || spec.Tier == ConsequenceTier.Tier0)
            return;
        if (!_handlers.TryGetValue(spec.Tier, out var handler))
        {
            _monitor.Log($"ConsequenceEngine: no handler registered for tier {spec.Tier}; skipping.", LogLevel.Warn);
            return;
        }

        var (loved, hated) = ResolveAffectedNpcs(spec);
        var ctx = new ConsequenceContext(spec, _config, _scanner, _state, _monitor, loved, hated);
        try
        {
            handler.Apply(ctx);
        }
        catch (Exception ex)
        {
            _monitor.Log($"ConsequenceEngine: handler for tier {spec.Tier} threw {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
        }
    }

    private (IReadOnlyList<string> loved, IReadOnlyList<string> hated) ResolveAffectedNpcs(ConsequenceSpec spec)
    {
        // Static-source quests treat `Targets[]` as the affected pool (today's CSV
        /// only uses static for negative reactions, so we route them into `hated`).
        // Tier 3 chain consequences (Seafood Night, etc.) want every static target to
        // get a chained dialogue, so we don't sample down on this path.
        if (spec.Source == ConsequenceSource.Static)
            return (Array.Empty<string>(), MetOnly(spec.Targets));

        // GiftTastes: scan `Data/NPCGiftTastes` for NPCs whose loved / hated list
        // mentions the subject id. Filter to met NPCs so we never queue a line for
        // someone the player hasn't actually encountered.
        var loved = MetOnly(_scanner.NpcsWhoLove(spec.Subject));
        var hated = MetOnly(_scanner.NpcsWhoHate(spec.Subject));
        if (spec.Targets.Count > 0)
        {
            // Authors can append a fixed NPC even when scanning tastes, useful for the
            // dispatcher's NPC if they aren't covered by the taste row.
            var extra = MetOnly(spec.Targets);
            var merged = new List<string>(hated.Count + extra.Count);
            merged.AddRange(hated);
            foreach (var n in extra)
                if (!Contains(merged, n))
                    merged.Add(n);
            hated = merged;
        }

        // Sample down to one NPC per side independently. Queueing a line for every
        // loved + every hated NPC produces a fatigue-inducing wall of reactions over
        // the next in-game week; sampling per-side keeps the loved + hated narrative
        // beats both visible (so the player gets to feel the trade-off) without
        // spamming. Tier 3 chains (Static source above) are exempt, those want every
        // ecology NPC to react over multiple days.
        return SampleEachSide(loved, hated);
    }

    /// Picks at most one NPC from each pool independently. So a quest with both
    /// loved-by + hated-by NPCs surfaces exactly two reactions (one positive, one
    /// negative); a quest with only one side populated surfaces just that side.
    private static (IReadOnlyList<string> loved, IReadOnlyList<string> hated) SampleEachSide(IReadOnlyList<string> loved, IReadOnlyList<string> hated)
    {
        var rng = StardewValley.Game1.random;
        IReadOnlyList<string> sampledLoved = loved.Count == 0
            ? Array.Empty<string>()
            : new[] { loved[rng.Next(loved.Count)] };
        IReadOnlyList<string> sampledHated = hated.Count == 0
            ? Array.Empty<string>()
            : new[] { hated[rng.Next(hated.Count)] };
        return (sampledLoved, sampledHated);
    }

    private static List<string> MetOnly(IReadOnlyList<string> input)
    {
        var output = new List<string>(input.Count);
        var friendship = StardewValley.Game1.player?.friendshipData;
        if (friendship == null)
            return output;
        foreach (var name in input)
        {
            if (friendship.ContainsKey(name))
                output.Add(name);
        }
        return output;
    }

    private static bool Contains(List<string> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i], name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
