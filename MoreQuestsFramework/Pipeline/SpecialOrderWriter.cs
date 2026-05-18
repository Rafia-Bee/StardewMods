using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.SpecialOrders;
using StardewValley.SpecialOrders;

namespace MoreQuestsFramework.Pipeline;

// OrderId is namespaced as <ownerUniqueId>.<defId>.<dayStamp> so vanilla/third-party
// SpecialOrders keys can't collide. SweepExpired only drops entries past expiry that
// aren't in-flight in team.specialOrders, so accepted orders aren't yanked.
internal sealed class SpecialOrderWriter
{
    private const string AssetName = "Data/SpecialOrders";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private FrameworkState? _state;
    private bool _registered;

    public SpecialOrderWriter(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void WireState(FrameworkState state)
    {
        _state = state;
    }

    public IReadOnlyList<EmittedSpecialOrder> EmittedOrders =>
        _state?.EmittedSpecialOrders ?? (IReadOnlyList<EmittedSpecialOrder>)Array.Empty<EmittedSpecialOrder>();

    public void Register()
    {
        if (_registered)
            return;
        _helper.Events.Content.AssetRequested += OnAssetRequested;
        _registered = true;
    }

    public void Emit(QuestPosting posting)
    {
        if (_state == null)
        {
            _monitor.Log("SpecialOrderWriter not yet wired to state; dropping emit.", LogLevel.Warn);
            return;
        }
        var spec = posting.SpecialOrder;
        if (spec == null)
        {
            _monitor.Log($"SpecialOrder posting '{posting.DefinitionId}' carries no SpecialOrder spec; nothing to emit.", LogLevel.Warn);
            return;
        }

        int today = Game1.Date.TotalDays;
        string orderId = BuildOrderId(posting.OwnerUniqueId, posting.DefinitionId, today);
        int durationDays = ResolveDurationDays(spec.Duration);
        // +7 grace so accepting on the last offered day still leaves full duration.
        int expiresAfterDay = today + durationDays + 7;

        _state.EmittedSpecialOrders.RemoveAll(e => e.DefinitionId == posting.DefinitionId);

        var entry = new EmittedSpecialOrder
        {
            OrderId = orderId,
            EmittedDay = today,
            ExpiresAfterDay = expiresAfterDay,
            OwnerUniqueId = posting.OwnerUniqueId,
            DefinitionId = posting.DefinitionId,
            Spec = spec
        };
        _state.EmittedSpecialOrders.Add(entry);

        _helper.GameContent.InvalidateCache(AssetName);
        ModEntry.LogDebug($"Emitted SpecialOrder '{orderId}' (definition '{posting.DefinitionId}', duration '{spec.Duration}').");
    }

    public void SweepExpired()
    {
        if (_state == null)
            return;
        if (_state.EmittedSpecialOrders.Count == 0)
        {
            // No active orders means any leftover bookkeeping is stale (a save imported from
            // an older build, or a path that registered a grant without an emit).
            if (_state.FrameworkRewardsGranted.Count > 0)
                _state.FrameworkRewardsGranted.Clear();
            return;
        }

        int today = Game1.Date.TotalDays;
        var inFlight = new HashSet<string>(StringComparer.Ordinal);
        var team = Game1.player?.team;
        if (team != null)
        {
            foreach (var so in team.specialOrders)
                if (so?.questKey?.Value != null)
                    inFlight.Add(so.questKey.Value);
        }

        int dropped = _state.EmittedSpecialOrders.RemoveAll(e =>
        {
            if (e.ExpiresAfterDay > today || inFlight.Contains(e.OrderId))
                return false;
            _state.FrameworkRewardsGranted.Remove(e.OrderId);
            return true;
        });

        // Belt-and-suspenders: ensure FrameworkRewardsGranted never carries an entry
        // without a matching emitted order, so the list stays bounded by what's tracked.
        if (_state.FrameworkRewardsGranted.Count > 0)
        {
            var validIds = new HashSet<string>(_state.EmittedSpecialOrders.Count, StringComparer.Ordinal);
            foreach (var e in _state.EmittedSpecialOrders)
                validIds.Add(e.OrderId);
            _state.FrameworkRewardsGranted.RemoveAll(id => !validIds.Contains(id));
        }

        if (dropped > 0)
        {
            _helper.GameContent.InvalidateCache(AssetName);
            ModEntry.LogDebug($"Swept {dropped} expired SpecialOrder entr{(dropped == 1 ? "y" : "ies")}.");
        }
    }

    // Idempotent via FrameworkRewardsGranted (dedups across ticks and save/load). Bypasses
    // Data/SpecialOrders.Rewards entirely. Money stays in the vanilla path because the
    // reward UI handles it natively.
    public void CheckCompletionsAndGrantRewards()
    {
        if (_state == null || _state.EmittedSpecialOrders.Count == 0)
            return;
        var team = Game1.player?.team;
        if (team == null || team.specialOrders.Count == 0)
            return;

        var byOrderId = new Dictionary<string, EmittedSpecialOrder>(StringComparer.Ordinal);
        foreach (var e in _state.EmittedSpecialOrders)
            byOrderId[e.OrderId] = e;

        var alreadyGranted = new HashSet<string>(_state.FrameworkRewardsGranted, StringComparer.Ordinal);

        foreach (var order in team.specialOrders)
        {
            string? key = order?.questKey?.Value;
            if (string.IsNullOrEmpty(key))
                continue;
            if (order!.questState.Value != SpecialOrderStatus.Complete)
                continue;
            if (alreadyGranted.Contains(key))
                continue;
            if (!byOrderId.TryGetValue(key, out var emitted))
                continue;
            if (emitted.Spec.FrameworkRewards.Count == 0 && emitted.Spec.Consequences.Count == 0)
            {
                _state.FrameworkRewardsGranted.Add(key);
                continue;
            }

            try
            {
                if (emitted.Spec.FrameworkRewards.Count > 0)
                    Rewards.RewardApplier.Apply(emitted.Spec.FrameworkRewards);

                int firedConsequences = 0;
                if (emitted.Spec.Consequences.Count > 0 && ConsequenceEngine.Active != null)
                {
                    foreach (var spec in emitted.Spec.Consequences)
                    {
                        if (spec == null || spec.Tier == ConsequenceTier.Tier0)
                            continue;
                        ConsequenceEngine.Active.Apply(spec);
                        firedConsequences++;
                    }
                }

                _state.FrameworkRewardsGranted.Add(key);
                ModEntry.LogDebug($"Granted framework payload for completed SpecialOrder '{key}' ({emitted.Spec.FrameworkRewards.Count} reward(s), {firedConsequences} consequence(s)).");
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to grant framework payload for SpecialOrder '{key}': {ex.Message}", LogLevel.Warn);
            }
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!e.NameWithoutLocale.IsEquivalentTo(AssetName))
            return;
        if (_state == null || _state.EmittedSpecialOrders.Count == 0)
            return;

        e.Edit(asset =>
        {
            // AsDictionary<,> requires the exact value type (object won't cast).
            var dict = asset.AsDictionary<string, SpecialOrderData>().Data;
            foreach (var emitted in _state.EmittedSpecialOrders)
            {
                try
                {
                    dict[emitted.OrderId] = BuildVanillaOrder(emitted.Spec, emitted.OrderId, emitted.OwnerUniqueId);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Failed to inject SpecialOrder '{emitted.OrderId}': {ex.Message}", LogLevel.Warn);
                }
            }
        });
    }

    private static string BuildOrderId(string ownerUniqueId, string defId, int today)
    {
        // Daystamp suffix lets the same definition emit one entry per year (vanilla
        // won't accept two entries with the same key on the same load).
        string owner = string.IsNullOrEmpty(ownerUniqueId) ? "RafiaBee.MoreQuestsFramework" : ownerUniqueId;
        return $"{owner}.{defId}.{today}";
    }

    private static int ResolveDurationDays(string? duration)
    {
        return duration?.Trim().ToLowerInvariant() switch
        {
            "oneday" => 1,
            "twodays" => 2,
            "threedays" => 3,
            "twoweeks" => 14,
            "month" => 28,
            _ => 7
        };
    }

    // Built per edit-pass, not cached: vanilla mutates fields like Objectives during
    // GetSpecialOrder, so a cached instance would drift.
    private SpecialOrderData BuildVanillaOrder(SpecialOrderSpec spec, string orderId, string ownerUniqueId)
    {
        var data = new SpecialOrderData
        {
            Name = spec.Name,
            Requester = spec.Requester,
            Duration = ParseDuration(spec.Duration),
            Repeatable = false,
            RequiredTags = spec.RequiredTags ?? "",
            Condition = "",
            OrderType = spec.OrderType ?? "",
            SpecialRule = "",
            Text = spec.Text,
            ItemToRemoveOnEnd = null,
            MailToRemoveOnEnd = null,
            RandomizedElements = null,
            Objectives = new List<SpecialOrderObjectiveData>(spec.Objectives.Count),
            Rewards = new List<SpecialOrderRewardData>(spec.Rewards.Count),
            // Quest trackers read CustomFields for source attribution.
            CustomFields = new Dictionary<string, string>
            {
                ["FromMod"] = string.IsNullOrEmpty(ownerUniqueId) ? "RafiaBee.MoreQuestsFramework" : ownerUniqueId,
                ["FromFramework"] = "RafiaBee.MoreQuestsFramework"
            }
        };
        foreach (var o in spec.Objectives)
        {
            data.Objectives.Add(new SpecialOrderObjectiveData
            {
                Type = o.Type,
                Text = o.Text,
                RequiredCount = o.RequiredCount.ToString(),
                Data = new Dictionary<string, string>(o.Data ?? new Dictionary<string, string>())
            });
        }
        foreach (var r in spec.Rewards)
        {
            var rewardData = new Dictionary<string, string>(r.Data ?? new Dictionary<string, string>());
            data.Rewards.Add(new SpecialOrderRewardData
            {
                Type = r.Type,
                Data = rewardData
            });
            string dump = string.Join(", ", rewardData.Select(kv => $"{kv.Key}={kv.Value}"));
            ModEntry.LogDebug($"SpecialOrder '{orderId}' reward '{r.Type}' Data: [{dump}]");
        }
        return data;
    }

    private static QuestDuration ParseDuration(string? duration)
    {
        return duration?.Trim().ToLowerInvariant() switch
        {
            "oneday" => QuestDuration.OneDay,
            "twodays" => QuestDuration.TwoDays,
            "threedays" => QuestDuration.ThreeDays,
            "twoweeks" => QuestDuration.TwoWeeks,
            "month" => QuestDuration.Month,
            _ => QuestDuration.Week
        };
    }
}
