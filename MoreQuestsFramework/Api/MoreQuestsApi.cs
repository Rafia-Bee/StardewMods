using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Posting.Boards;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.State;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Must stay top-level: SMAPI's GetApi rejects API types where Type.IsPublic is false,
// which is the case for nested types regardless of declared accessibility.
public sealed class MoreQuestsApi : IMoreQuestsApi
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
    private readonly CustomStepRegistry _customSteps;
    private readonly CustomTriggerRegistry _customTriggers;
    private readonly CustomRewardRegistry _customRewards;
    private readonly CustomConditionRegistry _customConditions;
    private readonly CustomBoardQuestRegistry _customBoardQuests;
    private readonly DispatchRegistry _dispatch;
    private readonly BoardRegistry _boards;
    private readonly CombatFoodRegistry _combatFood;
    private readonly MailStashCodecRegistry _mailStashCodecs;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;
    private readonly Action _refreshOffers;
    private readonly Func<QuestContext?> _ctxProvider;

    private readonly ConditionalWeakTable<Quest, ManagedQuest> _managed = new();
    private readonly Dictionary<string, IMoreQuestsModApi> _modScopes
        = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? RegistrationOpen;
    public event EventHandler? RegistrationClosed;
    public event EventHandler<QuestAcceptedArgs>? QuestAccepted;
    public event EventHandler<QuestCompletedArgs>? QuestCompleted;
    public event EventHandler<QuestRemovedArgs>? QuestRemoved;
    public event EventHandler<DayRefreshedArgs>? DayRefreshed;
    public event EventHandler<QuestSkippedArgs>? QuestSkippedToday;
    public event EventHandler? FrameworkShuttingDown;

    private FrameworkState? _state;

    internal MoreQuestsApi(
        QuestRegistry registry,
        GeneratorRegistry generators,
        CustomStepRegistry customSteps,
        CustomTriggerRegistry customTriggers,
        CustomRewardRegistry customRewards,
        CustomConditionRegistry customConditions,
        CustomBoardQuestRegistry customBoardQuests,
        DispatchRegistry dispatch,
        BoardRegistry boards,
        CombatFoodRegistry combatFood,
        MailStashCodecRegistry mailStashCodecs,
        IMonitor monitor,
        Func<ISpaceCoreApi?> spaceCore,
        Action refreshOffers,
        Func<QuestContext?> ctxProvider)
    {
        _registry = registry;
        _generators = generators;
        _customSteps = customSteps;
        _customTriggers = customTriggers;
        _customRewards = customRewards;
        _customConditions = customConditions;
        _customBoardQuests = customBoardQuests;
        _dispatch = dispatch;
        _boards = boards;
        _combatFood = combatFood;
        _mailStashCodecs = mailStashCodecs;
        _monitor = monitor;
        _spaceCore = spaceCore;
        _refreshOffers = refreshOffers;
        _ctxProvider = ctxProvider;
    }

    public IMoreQuestsModApi GetModApi(IManifest mod)
    {
        if (mod == null)
            throw new ArgumentNullException(nameof(mod));
        if (_modScopes.TryGetValue(mod.UniqueID, out var existing))
            return existing;
        var scope = new MoreQuestsModApi(mod, _registry, _generators, _customSteps, _customTriggers, _customRewards, _customConditions, _customBoardQuests, _dispatch, _boards, _mailStashCodecs, _monitor, _spaceCore, ResolveQuestOwner);
        _modScopes[mod.UniqueID] = scope;
        return scope;
    }

    public bool IsManagedQuest(Quest quest) =>
        quest != null && _managed.TryGetValue(quest, out _);

    public int? GetDeliveredQuality(Quest quest)
    {
        return quest is MoreQuestsItemDeliveryQuest idq
            ? idq.deliveredQuality.Value
            : null;
    }

    public string? GetDefinitionId(Quest quest)
    {
        return TryGetManaged(quest, out var info) ? info.DefinitionId : null;
    }

    public IReadOnlyList<string> RegisteredQuestIds() => _registry.RegisteredIds();

    public bool? IsQuestAvailable(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return null;
        if (!_registry.TryGet(definitionId, out var def) || def == null)
            return null;
        var ctx = _ctxProvider();
        if (ctx == null)
            return null;
        return def.IsAvailable(ctx);
    }

    public QuestInfo? GetQuestInfo(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return null;
        if (!_registry.TryGet(definitionId, out var def) || def == null)
            return null;
        return new QuestInfo(
            def.Id,
            def.OwnerUniqueId,
            def.Category,
            def.Kind,
            def.Source,
            _registry.EffectiveSource(def));
    }

    public int? GetLastFiredDay(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId) || _state == null)
            return null;
        if (!_registry.TryGet(definitionId, out _))
            return null;
        return _state.LastFiredDay.TryGetValue(definitionId, out int day) ? day : (int?)null;
    }

    public bool? GetOneShotFired(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId) || _state == null)
            return null;
        if (!_registry.TryGet(definitionId, out _))
            return null;
        return _state.OneShotFired.TryGetValue(definitionId, out bool fired) && fired;
    }

    public IReadOnlyList<CustomBoardSlotInfo> GetCustomBoardSlots(string? boardOwnerUniqueId = null, string? boardName = null)
    {
        var list = new List<CustomBoardSlotInfo>();
        bool filtered = !string.IsNullOrEmpty(boardOwnerUniqueId) && !string.IsNullOrEmpty(boardName);
        foreach (var slot in CustomBoardSlots.AllSlots())
        {
            if (filtered)
            {
                string key = (boardOwnerUniqueId ?? "") + "/" + (boardName ?? "");
                if (!string.Equals(slot.BoardKey, key, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            int slash = slot.BoardKey.IndexOf('/');
            string owner = slash >= 0 ? slot.BoardKey.Substring(0, slash) : "";
            string name = slash >= 0 ? slot.BoardKey.Substring(slash + 1) : slot.BoardKey;
            list.Add(new CustomBoardSlotInfo(
                slot.SyncId,
                slot.Quest,
                owner,
                name,
                slot.Posting.DefinitionId,
                slot.Posting.OwnerUniqueId,
                slot.Accepted));
        }
        return list;
    }

    // Called from ModEntry once StateStore has finished loading. Before this call,
    // the LastFiredDay / OneShotFired lookups return null (no save loaded yet).
    internal void WireState(FrameworkState state) => _state = state;

    internal void ClearState() => _state = null;

    internal CustomStepRegistry CustomSteps => _customSteps;
    internal CustomTriggerRegistry CustomTriggers => _customTriggers;
    internal CustomRewardRegistry CustomRewards => _customRewards;
    internal CustomConditionRegistry CustomConditions => _customConditions;
    internal CustomBoardQuestRegistry CustomBoardQuests => _customBoardQuests;

    public void RefreshOffers() => _refreshOffers();

    public string? PickDispatchNpc(string role) => _dispatch.Pick(role);

    public IReadOnlyList<string> GetDispatchPool(string role) => _dispatch.ResolvePool(role);

    public IReadOnlyList<string> GetMetHumanNpcs() => DispatchRegistry.MetHumanNpcs();

    public void RegisterCombatFood(string itemId, int? magnitude = null) => _combatFood.Register(itemId, magnitude);

    public IReadOnlyList<string> GetCombatFoodPool() => _combatFood.Pool;

    public int? GetCombatFoodMagnitude(string qualifiedItemId) => _combatFood.GetMagnitude(qualifiedItemId);

    internal void TrackPosted(Quest quest, string ownerUniqueId, string definitionId)
    {
        if (quest == null)
            return;
        if (_managed.TryGetValue(quest, out _))
            return;
        _managed.Add(quest, new ManagedQuest(ownerUniqueId, definitionId));
    }

    internal bool TryGetManaged(Quest quest, out ManagedQuest info)
    {
        if (quest != null && _managed.TryGetValue(quest, out var found))
        {
            info = found;
            return true;
        }
        info = default!;
        return false;
    }

    private string? ResolveQuestOwner(Quest quest)
        => TryGetManaged(quest, out var info) ? info.OwnerUniqueId : null;

    internal void FireRegistrationOpen() => RegistrationOpen?.Invoke(this, EventArgs.Empty);
    internal void FireRegistrationClosed() => RegistrationClosed?.Invoke(this, EventArgs.Empty);
    internal void FireQuestAccepted(Quest q, ManagedQuest info)
        => QuestAccepted?.Invoke(this, new QuestAcceptedArgs(q, info.OwnerUniqueId, info.DefinitionId));
    internal void FireQuestCompleted(Quest q, ManagedQuest info)
        => QuestCompleted?.Invoke(this, new QuestCompletedArgs(q, info.OwnerUniqueId, info.DefinitionId));
    internal void FireQuestRemoved(Quest q, ManagedQuest info, QuestRemovalReason reason)
        => QuestRemoved?.Invoke(this, new QuestRemovedArgs(q, info.OwnerUniqueId, info.DefinitionId, reason));
    internal void FireDayRefreshed(int dailyCount, int mailCount)
        => DayRefreshed?.Invoke(this, new DayRefreshedArgs(dailyCount, mailCount));
    internal void FireQuestSkippedToday(string defId, string ownerUniqueId, TriggerSource source, QuestSkipReason reason)
        => QuestSkippedToday?.Invoke(this, new QuestSkippedArgs(defId, ownerUniqueId, source, reason));
    internal void FireFrameworkShuttingDown()
        => FrameworkShuttingDown?.Invoke(this, EventArgs.Empty);

    internal sealed class ManagedQuest
    {
        public string OwnerUniqueId { get; }
        public string DefinitionId { get; }
        public ManagedQuest(string ownerUniqueId, string definitionId)
        {
            OwnerUniqueId = ownerUniqueId;
            DefinitionId = definitionId;
        }
    }
}
