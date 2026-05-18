using System;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Rewards;

internal sealed class FairStarTokensWriter
{
    private const int LookaheadDays = 30;

    private readonly IMonitor _monitor;
    private FrameworkState? _state;

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

        ModEntry.LogDebug($"FairStarTokens granted: +{reward.Amount} until day {expiresAfter}.");
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
