using System;
using System.Collections.Generic;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

// Names are namespaced as {ownerUniqueId}/{name}, mirroring GeneratorRegistry. A
// Custom AdventureStep's Targets[0] is the handler id (literal "OtherMod/Name" or
// a bare name resolved against the owning consumer mod's UniqueID).
internal sealed class CustomStepRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, Func<CustomStepContext, int>> _handlers
        = new(StringComparer.OrdinalIgnoreCase);

    public CustomStepRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string ownerUniqueId, string name, Func<CustomStepContext, int> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _monitor.Log($"CustomStepRegistry rejected registration from '{ownerUniqueId}': name is empty.", LogLevel.Warn);
            return;
        }
        if (handler == null)
        {
            _monitor.Log($"CustomStepRegistry rejected '{name}' from '{ownerUniqueId}': handler is null.", LogLevel.Warn);
            return;
        }
        string fq = Qualify(ownerUniqueId, name);
        if (_handlers.ContainsKey(fq))
        {
            _monitor.Log($"CustomStepRegistry rejected duplicate handler '{fq}'.", LogLevel.Warn);
            return;
        }
        _handlers[fq] = handler;
    }

    public Func<CustomStepContext, int>? Resolve(string ownerUniqueId, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (_handlers.TryGetValue(name, out var direct))
            return direct;
        string fq = Qualify(ownerUniqueId, name);
        return _handlers.TryGetValue(fq, out var scoped) ? scoped : null;
    }

    private static string Qualify(string ownerUniqueId, string name)
        => name.Contains('/') ? name : $"{ownerUniqueId}/{name}";
}
