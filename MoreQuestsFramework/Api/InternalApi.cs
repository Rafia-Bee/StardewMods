using System;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Api;

/// SMAPI's `GetApi`/`GetApi<T>` paths reject API instances whose concrete type isn't
/// `Type.IsPublic`. `IsPublic` returns `false` for nested types regardless of their
/// declared accessibility — only top-level types qualify. Keep this class top-level.
public sealed class InternalApi : IInternalApi
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
    private readonly QuestPackLoader _loader;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;
    private readonly Action _refreshOffers;

    public InternalApi(
        QuestRegistry registry,
        GeneratorRegistry generators,
        QuestPackLoader loader,
        IMonitor monitor,
        Func<ISpaceCoreApi?> spaceCore,
        Action refreshOffers)
    {
        _registry = registry;
        _generators = generators;
        _loader = loader;
        _monitor = monitor;
        _spaceCore = spaceCore;
        _refreshOffers = refreshOffers;
    }

    public void RegisterQuest(IQuestDefinition definition) => _registry.Register(definition);

    public void RegisterCustomQuestType(Type questType)
    {
        var sc = _spaceCore();
        if (sc == null)
        {
            _monitor.Log(
                $"RegisterCustomQuestType('{questType.Name}') skipped: SpaceCore not loaded. " +
                "Custom Quest subclass will not survive a save/load round-trip.",
                LogLevel.Warn);
            return;
        }
        sc.RegisterSerializerType(questType);
        _monitor.Log($"Registered custom Quest type '{questType.Name}' with SpaceCore.", LogLevel.Trace);
    }

    public void RegisterGenerator(IManifest owner, string name, Func<QuestContext, QuestPosting?> generator)
        => _generators.Register(owner.UniqueID, name, generator);

    public void LoadContentPack(IContentPack pack) => _loader.LoadContentPack(pack);

    public void LoadQuestsFromMod(IModHelper helper, IManifest manifest, string relativePath)
        => _loader.LoadFromMod(helper, manifest, relativePath);

    public void RefreshOffers() => _refreshOffers();
}
