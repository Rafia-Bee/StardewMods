using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

/// SMAPI's `GetApi`/`GetApi&lt;T&gt;` paths reject API instances whose concrete type isn't
/// `Type.IsPublic`. `IsPublic` returns `false` for nested types regardless of their
/// declared accessibility, only top-level types qualify. Keep this class top-level.
public sealed class MoreQuestsApi : IMoreQuestsApi
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
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
        var scope = new MoreQuestsModApi(mod, _registry, _generators, _loader, _dispatch, _boards, _boardLoader, _monitor, _spaceCore);
        _modScopes[mod.UniqueID] = scope;
        return scope;
    }

    public bool IsManagedQuest(Quest quest) =>
        quest != null && _managed.TryGetValue(quest, out _);

    public void RefreshOffers() => _refreshOffers();

    public void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null)
        => _dispatch.Register(role, npcName, requiredModUniqueId);

    public string? PickDispatchNpc(string role) => _dispatch.Pick(role);

    public IReadOnlyList<string> GetDispatchPool(string role) => _dispatch.ResolvePool(role);

    public IReadOnlyList<string> GetMetHumanNpcs() => DispatchRegistry.MetHumanNpcs();

    public void RegisterCombatFood(string itemId) => _combatFood.Register(itemId);

    public IReadOnlyList<string> GetCombatFoodPool() => _combatFood.Pool;

    // --- Internal hooks called by framework code ---

    /// Tracks a Quest the framework just posted. The owner UniqueID + definition ID
    /// flow into subsequent `QuestAccepted` / `QuestCompleted` / `QuestRemoved` events.
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
