using System;
using System.Collections.Generic;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Api;

public sealed class MoreQuestsModApi : IMoreQuestsModApi
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
    private readonly CustomStepRegistry _customSteps;
    private readonly QuestPackLoader _loader;
    private readonly DispatchRegistry _dispatch;
    private readonly BoardRegistry _boards;
    private readonly BoardPackLoader _boardLoader;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;

    public IManifest Owner { get; }

    public MoreQuestsModApi(
        IManifest owner,
        QuestRegistry registry,
        GeneratorRegistry generators,
        CustomStepRegistry customSteps,
        QuestPackLoader loader,
        DispatchRegistry dispatch,
        BoardRegistry boards,
        BoardPackLoader boardLoader,
        IMonitor monitor,
        Func<ISpaceCoreApi?> spaceCore)
    {
        Owner = owner;
        _registry = registry;
        _generators = generators;
        _customSteps = customSteps;
        _loader = loader;
        _dispatch = dispatch;
        _boards = boards;
        _boardLoader = boardLoader;
        _monitor = monitor;
        _spaceCore = spaceCore;
    }

    public void RegisterQuest(IQuestDefinition definition) => _registry.Register(definition);

    public void RegisterCustomQuestType(Type questType)
    {
        var sc = _spaceCore();
        if (sc == null)
        {
            _monitor.Log(
                $"RegisterCustomQuestType('{questType.Name}') from '{Owner.UniqueID}' skipped: SpaceCore not loaded. " +
                "Custom Quest subclass will not survive a save/load round-trip.",
                LogLevel.Warn);
            return;
        }
        sc.RegisterSerializerType(questType);
        _monitor.Log($"Registered custom Quest type '{questType.Name}' from '{Owner.UniqueID}' with SpaceCore.", LogLevel.Trace);
    }

    public void RegisterGenerator(string name, Func<QuestContext, QuestPosting?> generator)
        => _generators.Register(Owner.UniqueID, name, generator);

    public void RegisterCustomAdventureStep(string name, Func<CustomStepContext, int> handler)
        => _customSteps.Register(Owner.UniqueID, name, handler);

    public void LoadContentPack(IContentPack pack) => _loader.LoadContentPack(pack);

    public void LoadContentPack(IContentPack pack, Func<string, int?> cooldownTierResolver)
        => _loader.LoadContentPack(pack, cooldownTierResolver);

    public void LoadQuestsFromMod(IModHelper helper, string relativePath)
        => _loader.LoadFromMod(helper, Owner, relativePath);

    public void LoadQuestsFromMod(IModHelper helper, string relativePath, Func<string, int?> cooldownTierResolver)
        => _loader.LoadFromMod(helper, Owner, relativePath, cooldownTierResolver);

    public void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null)
        => _dispatch.Register(role, npcName, requiredModUniqueId);

    public void RegisterBoard(BoardDefinition board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));
        _boards.Register(board, Owner.UniqueID);
    }

    public void LoadBoardsFromMod(IModHelper helper, string relativePath)
        => _boardLoader.LoadFromMod(helper, Owner, relativePath);

    public BoardDefinition? FindBoard(string name)
        => _boards.Find(Owner.UniqueID, name);

    public void OverrideTriggerSource(string definitionId, TriggerSource source)
        => _registry.OverrideSource(definitionId, source);

    public void RegisterConsequenceTier(ConsequenceTier tier, IConsequenceHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        // Buffer + live-apply so consumer-mod registrations during RegistrationOpen
        // (before any save loads) carry through every subsequent save's engine.
        ConsequenceOverrides.Set(tier, handler);
        ConsequenceEngine.Active?.Register(tier, handler);
    }
}

internal static class ConsequenceOverrides
{
    private static readonly Dictionary<ConsequenceTier, IConsequenceHandler> _overrides = new();

    public static void Set(ConsequenceTier tier, IConsequenceHandler handler)
        => _overrides[tier] = handler;

    public static void ApplyTo(ConsequenceEngine engine)
    {
        foreach (var (tier, handler) in _overrides)
            engine.Register(tier, handler);
    }
}
