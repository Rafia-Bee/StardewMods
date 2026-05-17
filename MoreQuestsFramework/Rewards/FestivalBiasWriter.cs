using System;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Rewards;

internal sealed class FestivalBiasWriter
{
    private readonly IMonitor _monitor;
    private FrameworkState? _state;

    // Static handle so the festival patches don't need an instance reference threaded
    // through every Harmony postfix. Reset to null on save unload.
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

    // Idempotent on Festival: re-grant sums into the existing entry's magnitude.
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

    public void Consume(FestivalKind festival)
    {
        if (_state == null || _state.ActiveFestivalBiases.Count == 0)
            return;
        string key = festival.ToString();
        _state.ActiveFestivalBiases.RemoveAll(b =>
            string.Equals(b.Festival, key, StringComparison.OrdinalIgnoreCase));
    }

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

    // Generous padding so a quest completed early in the season keeps the bias alive
    // through the festival even on saves where the player was offline for a stretch.
    private static int LookaheadDaysFor(FestivalKind festival) => festival switch
    {
        FestivalKind.Luau => 30,
        FestivalKind.Fair => 30,
        _ => 14
    };
}
