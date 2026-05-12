using System;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Rewards;

/// Owns the framework's pending Stardew Valley Fair star-token grants. When `RewardApplier`
/// grants a `FairStarTokensReward`, the amount is queued in `FrameworkState.ActiveFairStarTokens`
/// with an expiry well past the Fair so the grant survives save/reload between quest
/// completion and Fall 16. The framework's once-per-second tick consumes the queue the
/// first time the Fair festival event is live, adding the amount to
/// `Game1.player.festivalScore`.
///
/// Sweep happens on `DayStarted` from `ModEntry`; entries past their `ExpiresAfterDay`
/// are dropped so a stale unused grant doesn't sit on the save forever.
public sealed class FairStarTokensWriter
{
    /// Generous lookahead: any quest that grants this reward triggers between Fall 12
    /// and Fall 15. Padding to 30 days covers a player completing very early in the
    /// season plus the festival day itself.
    private const int LookaheadDays = 30;

    private readonly IMonitor _monitor;
    private FrameworkState? _state;

    /// Static handle used by the framework tick so it doesn't need an instance reference
    /// threaded through every consumer. Reset to null on save unload.
    public static FairStarTokensWriter? Active { get; private set; }

    public FairStarTokensWriter(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void WireState(FrameworkState state)
    {
        _state = state;
        Active = this;
        RewardApplier.OnFairStarTokensGranted = Grant;
    }

    public void Grant(FairStarTokensReward reward)
    {
        if (_state == null)
        {
            _monitor.Log("FairStarTokens grant dropped: state not wired (no save loaded).", LogLevel.Warn);
            return;
        }
        if (reward.Amount <= 0)
            return;

        int today = Game1.Date?.TotalDays ?? 0;
        int expiresAfter = today + LookaheadDays;

        bool merged = false;
        foreach (var entry in _state.ActiveFairStarTokens)
        {
            entry.Amount += reward.Amount;
            entry.ExpiresAfterDay = Math.Max(entry.ExpiresAfterDay, expiresAfter);
            merged = true;
            break;
        }
        if (!merged)
        {
            _state.ActiveFairStarTokens.Add(new ActiveFairStarTokens
            {
                Amount = reward.Amount,
                ExpiresAfterDay = expiresAfter
            });
        }

        _monitor.Log($"FairStarTokens granted: +{reward.Amount} until day {expiresAfter}.", LogLevel.Trace);
    }

    public int PeekAmount()
    {
        if (_state == null || _state.ActiveFairStarTokens.Count == 0)
            return 0;
        int total = 0;
        foreach (var entry in _state.ActiveFairStarTokens)
            total += entry.Amount;
        return total;
    }

    public void Consume()
    {
        if (_state == null)
            return;
        _state.ActiveFairStarTokens.Clear();
    }

    public void SweepExpired()
    {
        if (_state == null || _state.ActiveFairStarTokens.Count == 0)
            return;
        int today = Game1.Date?.TotalDays ?? 0;
        _state.ActiveFairStarTokens.RemoveAll(b => today > b.ExpiresAfterDay);
    }

    public static void ClearActive()
    {
        Active = null;
        RewardApplier.OnFairStarTokensGranted = null;
    }
}
