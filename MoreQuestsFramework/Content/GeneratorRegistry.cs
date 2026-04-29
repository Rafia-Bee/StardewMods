using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

/// Runtime map of named C# generators that JSON quest definitions can reference
/// via `"Generator": "<name>"` (plan.md §6). Names are namespaced as
/// `{ownerUniqueId}/{name}` so two mods can ship a generator called `EggDelivery`
/// without colliding.
public sealed class GeneratorRegistry
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

    /// Looks up a generator. The lookup tries the literal name first (lets a JSON
    /// quest reference a generator owned by another mod via `"OtherMod/Name"`),
    /// then falls back to `{ownerUniqueId}/{name}` for unqualified references.
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
