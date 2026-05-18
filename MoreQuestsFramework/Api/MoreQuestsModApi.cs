using System;
using System.Collections.Generic;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

internal sealed class MoreQuestsModApi : IMoreQuestsModApi
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
    private readonly CustomStepRegistry _customSteps;
    private readonly CustomTriggerRegistry _customTriggers;
    private readonly CustomRewardRegistry _customRewards;
    private readonly CustomConditionRegistry _customConditions;
    private readonly CustomBoardQuestRegistry _customBoardQuests;
    private readonly QuestPackLoader _loader;
    private readonly DispatchRegistry _dispatch;
    private readonly BoardRegistry _boards;
    private readonly BoardPackLoader _boardLoader;
    private readonly MailStashCodecRegistry _mailStashCodecs;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;
    private readonly Func<Quest, string?> _resolveQuestOwner;

    public IManifest Owner { get; }

    public MoreQuestsModApi(
        IManifest owner,
        QuestRegistry registry,
        GeneratorRegistry generators,
        CustomStepRegistry customSteps,
        CustomTriggerRegistry customTriggers,
        CustomRewardRegistry customRewards,
        CustomConditionRegistry customConditions,
        CustomBoardQuestRegistry customBoardQuests,
        QuestPackLoader loader,
        DispatchRegistry dispatch,
        BoardRegistry boards,
        BoardPackLoader boardLoader,
        MailStashCodecRegistry mailStashCodecs,
        IMonitor monitor,
        Func<ISpaceCoreApi?> spaceCore,
        Func<Quest, string?> resolveQuestOwner)
    {
        Owner = owner;
        _registry = registry;
        _generators = generators;
        _customSteps = customSteps;
        _customTriggers = customTriggers;
        _customRewards = customRewards;
        _customConditions = customConditions;
        _customBoardQuests = customBoardQuests;
        _loader = loader;
        _dispatch = dispatch;
        _boards = boards;
        _boardLoader = boardLoader;
        _mailStashCodecs = mailStashCodecs;
        _monitor = monitor;
        _spaceCore = spaceCore;
        _resolveQuestOwner = resolveQuestOwner;
    }

    public bool RegisterQuest(IQuestDefinition definition) => _registry.Register(definition);

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
        ModEntry.LogDebug($"Registered custom Quest type '{questType.Name}' from '{Owner.UniqueID}' with SpaceCore.");
    }

    public void RegisterGenerator(string name, Func<QuestContext, QuestPosting?> generator)
        => _generators.Register(Owner.UniqueID, name, generator);

    public void RegisterCustomAdventureStep(string name, Func<CustomStepContext, int> handler)
        => _customSteps.Register(Owner.UniqueID, name, handler);

    public IReadOnlyList<ICustomStepHandle> GetActiveCustomSteps(string handlerName)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
            return Array.Empty<ICustomStepHandle>();
        string fq = handlerName.Contains('/') ? handlerName : $"{Owner.UniqueID}/{handlerName}";
        var log = Game1.player?.questLog;
        if (log == null || log.Count == 0)
            return Array.Empty<ICustomStepHandle>();
        List<ICustomStepHandle>? results = null;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not AdventureQuest a || a.completed.Value)
                continue;
            string questOwner = _resolveQuestOwner(a) ?? Owner.UniqueID;
            foreach (var (idx, stepName, handler) in a.ActiveCustomStepInfos())
            {
                if (string.IsNullOrEmpty(handler))
                    continue;
                string stepFq = handler.Contains('/') ? handler : $"{questOwner}/{handler}";
                if (!string.Equals(stepFq, fq, StringComparison.OrdinalIgnoreCase))
                    continue;
                results ??= new List<ICustomStepHandle>();
                results.Add(new CustomStepHandle(a, idx, stepName, stepFq));
            }
        }
        return (IReadOnlyList<ICustomStepHandle>?)results ?? Array.Empty<ICustomStepHandle>();
    }

    public void RegisterCustomTrigger(string name, Func<CustomTriggerContext, bool> handler)
        => _customTriggers.Register(Owner.UniqueID, name, handler);

    public void RegisterCustomReward(
        string name,
        Action<string> apply,
        Func<string, string, ITranslationHelper, string>? summarize = null)
        => _customRewards.Register(
            Owner.UniqueID,
            name,
            payload => apply(payload),
            summarize == null ? null : new CustomRewardRegistry.SummarizeDelegate((p, g, t) => summarize(p, g, t)));

    public void RegisterCustomCondition(string key, Func<string, bool> evaluator)
        => _customConditions.Register(Owner.UniqueID, key, evaluator);

    public void RegisterCustomBoardQuestType(string name, Func<CustomBoardQuestContext, Quest?> handler)
        => _customBoardQuests.Register(Owner.UniqueID, name, handler);

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

    public void Unregister(string definitionId)
        => _registry.Unregister(definitionId);

    public void RegisterMailStashCodec(
        string kind,
        Type questType,
        Func<Quest, IList<string>> encode,
        Func<IList<string>, Quest?> decode)
        => _mailStashCodecs.Register(kind, questType, encode, decode);

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
