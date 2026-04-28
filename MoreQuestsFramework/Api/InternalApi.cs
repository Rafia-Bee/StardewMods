using System;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Api;

/// SMAPI's `GetApi`/`GetApi<T>` paths reject API instances whose concrete type isn't
/// `Type.IsPublic`. `IsPublic` returns `false` for nested types regardless of their
/// declared accessibility — only top-level types qualify. Keep this class top-level.
public sealed class InternalApi : IInternalApi
{
    private readonly QuestRegistry _registry;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;

    public InternalApi(QuestRegistry registry, IMonitor monitor, Func<ISpaceCoreApi?> spaceCore)
    {
        _registry = registry;
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
                $"RegisterCustomQuestType('{questType.Name}') skipped: SpaceCore not loaded. " +
                "Custom Quest subclass will not survive a save/load round-trip.",
                LogLevel.Warn);
            return;
        }
        sc.RegisterSerializerType(questType);
        _monitor.Log($"Registered custom Quest type '{questType.Name}' with SpaceCore.", LogLevel.Trace);
    }
}
