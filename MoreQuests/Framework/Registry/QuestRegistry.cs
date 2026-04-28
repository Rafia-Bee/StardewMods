using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;

namespace MoreQuests.Framework.Registry;

/// Mutable runtime registry of quest definitions. Replaces the static `BoardQuestRegistry.All`
/// array. Populated at GameLaunched by `MoreQuests.CorePack` (built-ins) and, in later phases,
/// by content packs and third-party mods through the public API.
internal sealed class QuestRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, IQuestDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IQuestDefinition> _ordered = new();
    private bool _frozen;

    public QuestRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public IReadOnlyList<IQuestDefinition> All => _ordered;

    public IEnumerable<IQuestDefinition> WithKind(PostingKind kind)
    {
        // Plain loop avoids LINQ allocation on the daily hot path.
        for (int i = 0; i < _ordered.Count; i++)
        {
            if (_ordered[i].Kind == kind)
                yield return _ordered[i];
        }
    }

    /// Adds a quest definition. Duplicate IDs are logged and rejected so a stray re-registration
    /// can never silently shadow an earlier definition.
    public void Register(IQuestDefinition def)
    {
        if (_frozen)
        {
            _monitor.Log($"QuestRegistry rejected '{def.Id}': registry is frozen for this session.", LogLevel.Warn);
            return;
        }
        if (_byId.ContainsKey(def.Id))
        {
            _monitor.Log($"QuestRegistry rejected duplicate registration for '{def.Id}'.", LogLevel.Warn);
            return;
        }
        _byId[def.Id] = def;
        _ordered.Add(def);
    }

    public bool TryGet(string id, out IQuestDefinition? def) => _byId.TryGetValue(id, out def!);

    /// Closes the registration window. Subsequent Register calls log and no-op. Intended to be
    /// invoked once content-pack and third-party registrations finish in Phase 5.
    public void Freeze() => _frozen = true;

    /// Test/debug helper. Resets the registry to empty so a fresh registration pass can run.
    public void Clear()
    {
        _byId.Clear();
        _ordered.Clear();
        _frozen = false;
    }

    /// Snapshot for diagnostics. Allocates; not for hot paths.
    public IReadOnlyList<string> RegisteredIds() => _ordered.Select(d => d.Id).ToList();
}
