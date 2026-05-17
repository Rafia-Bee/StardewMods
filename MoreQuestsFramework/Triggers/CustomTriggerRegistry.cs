using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoreQuestsFramework.Triggers;

// Names are namespaced as {ownerUniqueId}/{name}, mirroring GeneratorRegistry. A
// JSON quest's Trigger.Custom field carries the handler id (literal "OtherMod/Name"
// or a bare name resolved against the owning consumer mod's UniqueID).
public sealed class CustomTriggerRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, Func<CustomTriggerContext, bool>> _handlers
        = new(StringComparer.OrdinalIgnoreCase);

    public CustomTriggerRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string ownerUniqueId, string name, Func<CustomTriggerContext, bool> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _monitor.Log($"CustomTriggerRegistry rejected registration from '{ownerUniqueId}': name is empty.", LogLevel.Warn);
            return;
        }
        if (handler == null)
        {
            _monitor.Log($"CustomTriggerRegistry rejected '{name}' from '{ownerUniqueId}': handler is null.", LogLevel.Warn);
            return;
        }
        string fq = Qualify(ownerUniqueId, name);
        if (_handlers.ContainsKey(fq))
        {
            _monitor.Log($"CustomTriggerRegistry rejected duplicate handler '{fq}'.", LogLevel.Warn);
            return;
        }
        _handlers[fq] = handler;
    }

    public Func<CustomTriggerContext, bool>? Resolve(string ownerUniqueId, string name)
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
