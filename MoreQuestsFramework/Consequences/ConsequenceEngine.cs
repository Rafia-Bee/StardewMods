using System;
using System.Collections.Generic;
using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.State;
using StardewModdingAPI;

namespace MoreQuestsFramework.Consequences;

internal sealed class ConsequenceEngine
{
    private readonly Dictionary<ConsequenceTier, IConsequenceHandler> _handlers = new();
    private readonly MoreQuestsFrameworkConfig _config;
    private readonly GameDataCache _cache;
    private readonly GiftTastesScanner _scanner;
    private readonly FrameworkState _state;
    private readonly IMonitor _monitor;

    // Set by ModEntry.OnSaveLoaded so quest-subclass questComplete overrides can fire
    // the engine without threading an instance reference through every subclass.
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

    // Drops queued lines past the grace window. An NPC bringing up an overfishing
    // complaint a year later wouldn't read.
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
            ModEntry.LogDebug($"ConsequenceEngine: swept {dropped} stale pending dialogue entries (older than {grace} days past their fire day).");
    }

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
        // Static routes to hated (no positive static use case today). Tier 3 chains
        // come through here and rely on every static target reacting, no sampling.
        if (spec.Source == ConsequenceSource.Static)
        {
            // Static + no targets resolves to an empty hated list, so the handler runs
            // and quietly does nothing. Flag the no-op so authors don't lose hours
            // wondering why their consequence never lands.
            if (spec.Targets.Count == 0)
                _monitor.Log($"Consequence Source=Static with empty Targets has no NPCs to affect; tier {spec.Tier} will no-op.", LogLevel.Warn);
            return (Array.Empty<string>(), MetOnly(spec.Targets));
        }

        // Filter to met NPCs so we never queue a line for an unencountered one.
        var loved = MetOnly(_scanner.NpcsWhoLove(spec.Subject));
        var hated = MetOnly(_scanner.NpcsWhoHate(spec.Subject));
        if (spec.Targets.Count > 0)
        {
            var extra = MetOnly(spec.Targets);
            var merged = new List<string>(hated.Count + extra.Count);
            merged.AddRange(hated);
            foreach (var n in extra)
                if (!Contains(merged, n))
                    merged.Add(n);
            hated = merged;
        }

        // Sample one per side so the player feels both beats without a week-long
        // wall of reactions. Tier 3 (Static, above) is exempt.
        return SampleEachSide(loved, hated);
    }

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
