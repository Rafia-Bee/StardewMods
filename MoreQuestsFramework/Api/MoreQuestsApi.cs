using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Rewards;
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
    private readonly QuestPackLoader _loader;
    private readonly BoardPackLoader _boardLoader;
    private readonly DispatchRegistry _dispatch;
    private readonly BoardRegistry _boards;
    private readonly CombatFoodRegistry _combatFood;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;
    private readonly Action _refreshOffers;

    private readonly ConditionalWeakTable<Quest, ManagedQuest> _managed = new();
    private readonly Dictionary<string, IMoreQuestsModApi> _modScopes
        = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? RegistrationOpen;
    public event EventHandler? RegistrationClosed;
    public event EventHandler<QuestAcceptedArgs>? QuestAccepted;
    public event EventHandler<QuestCompletedArgs>? QuestCompleted;
    public event EventHandler<QuestRemovedArgs>? QuestRemoved;
    public event EventHandler<DayRefreshedArgs>? DayRefreshed;

    public MoreQuestsApi(
        QuestRegistry registry,
        GeneratorRegistry generators,
        CustomStepRegistry customSteps,
        CustomTriggerRegistry customTriggers,
        CustomRewardRegistry customRewards,
        CustomConditionRegistry customConditions,
        CustomBoardQuestRegistry customBoardQuests,
        QuestPackLoader loader,
        BoardPackLoader boardLoader,
        DispatchRegistry dispatch,
        BoardRegistry boards,
        CombatFoodRegistry combatFood,
        IMonitor monitor,
        Func<ISpaceCoreApi?> spaceCore,
        Action refreshOffers)
    {
        _registry = registry;
        _generators = generators;
        _customSteps = customSteps;
        _customTriggers = customTriggers;
        _customRewards = customRewards;
        _customConditions = customConditions;
        _customBoardQuests = customBoardQuests;
        _loader = loader;
        _boardLoader = boardLoader;
        _dispatch = dispatch;
        _boards = boards;
        _combatFood = combatFood;
        _monitor = monitor;
        _spaceCore = spaceCore;
        _refreshOffers = refreshOffers;
    }

    public IMoreQuestsModApi GetModApi(IManifest mod)
    {
        if (mod == null)
            throw new ArgumentNullException(nameof(mod));
        if (_modScopes.TryGetValue(mod.UniqueID, out var existing))
            return existing;
        var scope = new MoreQuestsModApi(mod, _registry, _generators, _customSteps, _customTriggers, _customRewards, _customConditions, _customBoardQuests, _loader, _dispatch, _boards, _boardLoader, _monitor, _spaceCore);
        _modScopes[mod.UniqueID] = scope;
        return scope;
    }

    public bool IsManagedQuest(Quest quest) =>
        quest != null && _managed.TryGetValue(quest, out _);

    internal CustomStepRegistry CustomSteps => _customSteps;
    internal CustomTriggerRegistry CustomTriggers => _customTriggers;
    internal CustomRewardRegistry CustomRewards => _customRewards;
    internal CustomConditionRegistry CustomConditions => _customConditions;
    internal CustomBoardQuestRegistry CustomBoardQuests => _customBoardQuests;

    public void RefreshOffers() => _refreshOffers();

    public void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null)
        => _dispatch.Register(role, npcName, requiredModUniqueId);

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

    internal void FireRegistrationOpen() => RegistrationOpen?.Invoke(this, EventArgs.Empty);
    internal void FireRegistrationClosed() => RegistrationClosed?.Invoke(this, EventArgs.Empty);
    internal void FireQuestAccepted(Quest q, ManagedQuest info)
        => QuestAccepted?.Invoke(this, new QuestAcceptedArgs(q, info.OwnerUniqueId, info.DefinitionId));
    internal void FireQuestCompleted(Quest q, ManagedQuest info)
        => QuestCompleted?.Invoke(this, new QuestCompletedArgs(q, info.OwnerUniqueId, info.DefinitionId));
    internal void FireQuestRemoved(Quest q, ManagedQuest info, bool wasCompleted)
        => QuestRemoved?.Invoke(this, new QuestRemovedArgs(q, info.OwnerUniqueId, info.DefinitionId, wasCompleted));
    internal void FireDayRefreshed(int dailyCount, int mailCount)
        => DayRefreshed?.Invoke(this, new DayRefreshedArgs(dailyCount, mailCount));

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
