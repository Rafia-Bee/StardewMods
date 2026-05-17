using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

// Names are namespaced as {ownerUniqueId}/{name}.
internal sealed class GeneratorRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, Func<QuestContext, QuestPosting?>> _generators
        = new(StringComparer.OrdinalIgnoreCase);

    public GeneratorRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string ownerUniqueId, string name, Func<QuestContext, QuestPosting?> generator)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _monitor.Log($"GeneratorRegistry rejected registration from '{ownerUniqueId}': name is empty.", LogLevel.Warn);
            return;
        }
        string fq = Qualify(ownerUniqueId, name);
        if (_generators.ContainsKey(fq))
        {
            _monitor.Log($"GeneratorRegistry rejected duplicate generator '{fq}'.", LogLevel.Warn);
            return;
        }
        _generators[fq] = generator;
    }

    // Tries the literal name first (so JSON can reference another mod's generator
    // via "OtherMod/Name"), then falls back to {ownerUniqueId}/{name}.
    public Func<QuestContext, QuestPosting?>? Resolve(string ownerUniqueId, string name)
    {
        if (_generators.TryGetValue(name, out var direct))
            return direct;
        string fq = Qualify(ownerUniqueId, name);
        return _generators.TryGetValue(fq, out var scoped) ? scoped : null;
    }

    private static string Qualify(string ownerUniqueId, string name)
        => name.Contains('/') ? name : $"{ownerUniqueId}/{name}";
}
