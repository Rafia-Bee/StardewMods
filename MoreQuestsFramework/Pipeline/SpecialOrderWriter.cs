using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.SpecialOrders;
using StardewValley.SpecialOrders;

namespace MoreQuestsFramework.Pipeline;

/// Owns the framework's edits to `Data/SpecialOrders`. Translates a `QuestPosting` whose
/// `Kind == SpecialOrder` into a vanilla `SpecialOrderData` JSON entry, stores it in
/// `FrameworkState.EmittedSpecialOrders`, and re-applies every emitted entry on every
/// `OnAssetRequested("Data/SpecialOrders")` pass so a SMAPI cache invalidation by any
/// other mod doesn't drop our edits.
///
/// Cleanup: at every `DayStarted` the writer sweeps entries whose `ExpiresAfterDay` has
/// passed and whose order isn't currently in `Game1.player.team.specialOrders` (so an
/// in-flight accepted order isn't pulled out from under the player). Completion events
/// are not emitted in 8a — vanilla owns the reward path end-to-end.
///
/// **Multi-mod safety:** every emitted order's `OrderId` is namespaced as
/// `<ownerUniqueId>.<defId>.<dayStamp>`, so collisions with vanilla or third-party
/// `Data/SpecialOrders` keys are essentially impossible. Cache invalidation is a re-edit
/// trigger, not a delete — other mods' entries remain intact across our invalidations.
public sealed class SpecialOrderWriter
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

    /// Wires the per-save state the writer reads from at every asset-edit pass. Called
    /// from `ModEntry.OnSaveLoaded` once the state is rehydrated.
    public void WireState(FrameworkState state)
    {
        _state = state;
    }

    /// Read-only view of every framework-emitted entry currently in `Data/SpecialOrders`.
    /// Consumers (the pagination patch) use this to guarantee framework-emitted orders
    /// surface on the board ahead of any random fill from the eligible pool.
    public IReadOnlyList<EmittedSpecialOrder> EmittedOrders =>
        _state?.EmittedSpecialOrders ?? (IReadOnlyList<EmittedSpecialOrder>)Array.Empty<EmittedSpecialOrder>();

    /// Registers the asset-edit handler. Idempotent; only ever subscribes once.
    public void Register()
    {
        if (_registered)
            return;
        _helper.Events.Content.AssetRequested += OnAssetRequested;
        _registered = true;
    }

    /// Translates a `QuestPosting` (Kind == SpecialOrder) into a `SpecialOrderData` JSON
    /// entry, persists it into save state, and invalidates the asset cache so the entry
    /// becomes visible to vanilla on the next read. No-op when the posting carries no
    /// `SpecialOrder` block.
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
        // Grace window so a player who accepts the order on the last offered day still has
        // vanilla's full duration to complete it before the writer evicts the entry.
        int expiresAfterDay = today + durationDays + 7;

        // Drop any prior emission for this definition (re-fire on the same day, or stale
        // entry from a previous attempt). Vanilla won't double-list a key, but we don't
        // want two stale dict entries hanging around either.
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
        _monitor.Log($"Emitted SpecialOrder '{orderId}' (definition '{posting.DefinitionId}', duration '{spec.Duration}').", LogLevel.Trace);
    }

    /// Drops every emitted entry whose expiry has passed and whose order isn't currently
    /// in-flight (`Game1.player.team.specialOrders`). Called once per day from `ModEntry.OnDayStarted`.
    public void SweepExpired()
    {
        if (_state == null || _state.EmittedSpecialOrders.Count == 0)
            return;

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
            // Also clear granted-rewards bookkeeping for the dropped entry so a future
            // re-emit of the same definition (next year, after cooldown) re-grants on
            // its own completion instead of being mistaken as already-granted.
            _state.FrameworkRewardsGranted.Remove(e.OrderId);
            return true;
        });

        if (dropped > 0)
        {
            _helper.GameContent.InvalidateCache(AssetName);
            _monitor.Log($"Swept {dropped} expired SpecialOrder entr{(dropped == 1 ? "y" : "ies")}.", LogLevel.Trace);
        }
    }

    /// Walks `Game1.player.team.specialOrders` once and applies framework-owned rewards
    /// (anything in `Spec.FrameworkRewards`) for any of our emitted entries that have
    /// flipped to `Complete` since the last check. Idempotent — `FrameworkRewardsGranted`
    /// dedups across ticks and across save/load. Called from the existing
    /// `ModEntry.OnOneSecondTick` so the grant fires within ~1s of completion.
    ///
    /// This path bypasses vanilla's `Data/SpecialOrders` Rewards array entirely, so any
    /// third-party content pack that mutates the asset's reward data (e.g. a friendship-
    /// configuration pack overwriting `Friendship` `OrderReward` entries) cannot intercept
    /// these. Money rewards are NOT applied here — they stay in the vanilla path because
    /// the player-facing reward UI handles them and the user's modset hasn't shown any
    /// interception of money.
    public void CheckCompletionsAndGrantRewards()
    {
        if (_state == null || _state.EmittedSpecialOrders.Count == 0)
            return;
        var team = Game1.player?.team;
        if (team == null || team.specialOrders.Count == 0)
            return;

        // Index emitted entries by orderId for O(1) lookup against the in-flight list.
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
            if (emitted.Spec.FrameworkRewards.Count == 0)
            {
                // Mark granted anyway so we don't keep re-checking an empty reward list.
                _state.FrameworkRewardsGranted.Add(key);
                continue;
            }

            try
            {
                Rewards.RewardApplier.Apply(emitted.Spec.FrameworkRewards);
                _state.FrameworkRewardsGranted.Add(key);
                _monitor.Log(
                    $"Granted framework rewards for completed SpecialOrder '{key}' ({emitted.Spec.FrameworkRewards.Count} reward(s)).",
                    LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to grant framework rewards for SpecialOrder '{key}': {ex.Message}", LogLevel.Warn);
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
            // `Data/SpecialOrders` is strongly-typed as Dictionary<string, SpecialOrderData>;
            // SMAPI's AsDictionary<,> requires the exact value type (object won't cast).
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
        // Two-segment namespace: owner UniqueID guarantees we don't collide with another
        // mod's order; the daystamp suffix lets the same definition emit one entry per
        // year (vanilla won't accept two entries with the same key on the same load).
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
            _ => 7 // Week (default) and any unrecognised value
        };
    }

    /// Constructs a vanilla `SpecialOrderData` instance from the framework-neutral
    /// `SpecialOrderSpec`. Built per asset-edit pass rather than cached so re-edits after
    /// a save reload always see fresh references — vanilla mutates fields like `Objectives`
    /// during `GetSpecialOrder`, so a cached instance would drift across reloads.
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
            // Quest-tracker mods commonly read CustomFields for source attribution. Filling
            // these explicitly lets trackers credit the order to the framework + owning
            // content mod instead of falling back to whatever heuristic they use (which can
            // pick a wrong mod when our orderId prefix doesn't match a UniqueID directly).
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
            // Diagnostic logging for the friendship-reward bug investigation. Logs each
            // reward type + its data dict at injection time so we can verify the round
            // trip from generator → save state → asset edit preserves every field.
            string dump = string.Join(", ", rewardData.Select(kv => $"{kv.Key}={kv.Value}"));
            _monitor.Log(
                $"SpecialOrder '{orderId}' reward '{r.Type}' Data: [{dump}]",
                LogLevel.Trace);
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
