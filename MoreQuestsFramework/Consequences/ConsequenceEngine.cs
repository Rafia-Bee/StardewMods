using System;
using System.Collections.Generic;
using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.State;
using StardewModdingAPI;

namespace MoreQuestsFramework.Consequences;

/// Routes every `ConsequenceSpec` to the matching `IConsequenceHandler`. One singleton
/// per save load — wired in `ModEntry.OnSaveLoaded` after the state store and gift-tastes
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
    /// reference through every subclass. Null when no save is loaded — the helper
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
            // Authors can append a fixed NPC even when scanning tastes — useful for the
            // dispatcher's NPC if they aren't covered by the taste row.
            var extra = MetOnly(spec.Targets);
            var merged = new List<string>(hated.Count + extra.Count);
            merged.AddRange(hated);
            foreach (var n in extra)
                if (!Contains(merged, n))
                    merged.Add(n);
            hated = merged;
        }

        // Sample down to a single representative across the union. Queueing a line
        // for every loved + every hated NPC produces a fatigue-inducing wall of
        // reactions over the next in-game week; one randomly-picked NPC tells the
        // narrative beat without spamming. Tier 3 chains (Static source above) are
        // exempt — those want every ecology NPC to react over multiple days.
        return SamplePair(loved, hated);
    }

    /// Picks at most one NPC across (loved ∪ hated). When both pools are non-empty
    /// the side is chosen 50/50 weighted by pool size (so a quest where only one NPC
    /// hates and ten NPCs love still leans toward the loved side most days, but the
    /// hated NPC still surfaces some of the time). Returns a tuple where the chosen
    /// side has one entry and the other is empty.
    private static (IReadOnlyList<string> loved, IReadOnlyList<string> hated) SamplePair(IReadOnlyList<string> loved, IReadOnlyList<string> hated)
    {
        int totalLoved = loved.Count;
        int totalHated = hated.Count;
        if (totalLoved == 0 && totalHated == 0)
            return (Array.Empty<string>(), Array.Empty<string>());

        var rng = StardewValley.Game1.random;
        int pick = rng.Next(totalLoved + totalHated);
        if (pick < totalLoved)
            return (new[] { loved[pick] }, Array.Empty<string>());
        return (Array.Empty<string>(), new[] { hated[pick - totalLoved] });
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
