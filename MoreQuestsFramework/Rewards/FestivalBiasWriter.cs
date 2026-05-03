using System;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Rewards;

/// Owns the framework's persisted festival biases. When `RewardApplier` grants a
/// `FestivalBiasReward`, the entry is appended to `FrameworkState.ActiveFestivalBiases`
/// with an expiry well past its festival day so the bias survives save/reload between
/// quest completion and the festival itself. The Luau / Fair Harmony patches read +
/// consume the relevant entry at the festival's judging hook.
///
/// Sweep happens on `DayStarted` from `ModEntry`; entries past their `ExpiresAfterDay`
/// are dropped so a stale unused bias doesn't sit on the save forever.
public sealed class FestivalBiasWriter
{
    private readonly IMonitor _monitor;
    private FrameworkState? _state;

    /// Static handle used by the festival patches so they don't need an instance reference
    /// threaded through every Harmony postfix. Reset to null on save unload.
    public static FestivalBiasWriter? Active { get; private set; }

    public FestivalBiasWriter(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void WireState(FrameworkState state)
    {
        _state = state;
        Active = this;
        RewardApplier.OnFestivalBiasGranted = Grant;
    }

    /// Records a new bias in save state. Idempotent on `Festival`: a re-grant for the same
    /// festival sums into the existing entry's magnitude rather than stacking a separate
    /// row, and extends the expiry to the later of the two.
    public void Grant(FestivalBiasReward reward)
    {
        if (_state == null)
        {
            _monitor.Log($"FestivalBias '{reward.Festival}' dropped: state not wired (no save loaded).", LogLevel.Warn);
            return;
        }
        if (reward.Magnitude <= 0)
            return;

        int today = Game1.Date?.TotalDays ?? 0;
        int expiresAfter = today + LookaheadDaysFor(reward.Festival);

        string festName = reward.Festival.ToString();
        bool merged = false;
        foreach (var b in _state.ActiveFestivalBiases)
        {
            if (!string.Equals(b.Festival, festName, StringComparison.OrdinalIgnoreCase))
                continue;
            b.Magnitude += reward.Magnitude;
            b.ExpiresAfterDay = Math.Max(b.ExpiresAfterDay, expiresAfter);
            merged = true;
            break;
        }
        if (!merged)
        {
            _state.ActiveFestivalBiases.Add(new ActiveFestivalBias
            {
                Festival = festName,
                Magnitude = reward.Magnitude,
                ExpiresAfterDay = expiresAfter
            });
        }

        _monitor.Log($"FestivalBias granted: +{reward.Magnitude} on {festName} until day {expiresAfter}.", LogLevel.Trace);
    }

    /// Returns the active bias magnitude for the given festival, or 0 when none. Cheap when
    /// the list is empty (the patches' fast path).
    public int PeekMagnitude(FestivalKind festival)
    {
        if (_state == null || _state.ActiveFestivalBiases.Count == 0)
            return 0;
        string key = festival.ToString();
        int total = 0;
        foreach (var b in _state.ActiveFestivalBiases)
        {
            if (string.Equals(b.Festival, key, StringComparison.OrdinalIgnoreCase))
                total += b.Magnitude;
        }
        return total;
    }

    /// Removes every active bias for `festival`. Called by the patches once the bias has
    /// been applied to that festival's judging so a re-judging in the same session (mod
    /// edge case) doesn't re-apply it.
    public void Consume(FestivalKind festival)
    {
        if (_state == null || _state.ActiveFestivalBiases.Count == 0)
            return;
        string key = festival.ToString();
        _state.ActiveFestivalBiases.RemoveAll(b =>
            string.Equals(b.Festival, key, StringComparison.OrdinalIgnoreCase));
    }

    /// Drops every entry whose `ExpiresAfterDay` is before today. Called from `DayStarted`.
    public void SweepExpired()
    {
        if (_state == null || _state.ActiveFestivalBiases.Count == 0)
            return;
        int today = Game1.Date?.TotalDays ?? 0;
        _state.ActiveFestivalBiases.RemoveAll(b => today > b.ExpiresAfterDay);
    }

    public static void ClearActive()
    {
        Active = null;
        RewardApplier.OnFestivalBiasGranted = null;
    }

    /// Days from grant to bias expiry. Generous on purpose — a quest completed early in
    /// the season needs the bias to survive every intervening day plus the festival day.
    /// Year-wraparound is handled by the totaldays count being monotonic.
    private static int LookaheadDaysFor(FestivalKind festival) => festival switch
    {
        // Luau is Summer 11 (day 39 of year 1). The Summer 8 trigger leaves three days; pad
        // generously so a quest completed at any point between trigger and festival keeps the
        // bias alive even on saves where the player was offline for a stretch.
        FestivalKind.Luau => 30,
        // Fair is Fall 16 (day 76 of year 1). Fall 8 trigger leaves eight days; same pad.
        FestivalKind.Fair => 30,
        _ => 14
    };
}
