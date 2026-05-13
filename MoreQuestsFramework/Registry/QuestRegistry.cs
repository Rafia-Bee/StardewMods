using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Registry;

/// Mutable runtime registry of quest definitions. Single source of truth for the engine.
/// Populated at GameLaunched by the framework (built-in vanilla wrappers) and by content
/// mods that fetch the framework's internal API and call `RegisterQuest(...)`.
public sealed class QuestRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, IQuestDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IQuestDefinition> _ordered = new();
    private readonly Dictionary<string, TriggerSource> _sourceOverrides = new(StringComparer.OrdinalIgnoreCase);
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

    /// Re-routes an already-registered quest to a different `TriggerSource` for the rest of
    /// the session. Used by content mods that want to flip their own quests between the
    /// help-wanted board and a custom board based on player config (e.g. an "enable
    /// Adventurer's Guild board" toggle that, when off, falls all guild-tagged quests back
    /// onto the daily board so the content stays reachable). The override is consulted by
    /// `QuestPipeline` instead of `def.Source` at posting time.
    ///
    /// Allowed at any point during the registration window AND after freeze, since the
    /// override is metadata, it doesn't add or remove definitions.
    public void OverrideSource(string definitionId, TriggerSource source)
    {
        if (string.IsNullOrEmpty(definitionId))
            return;
        if (!_byId.ContainsKey(definitionId))
        {
            _monitor.Log($"OverrideSource('{definitionId}', {source}) ignored: no such quest registered.", LogLevel.Warn);
            return;
        }
        _sourceOverrides[definitionId] = source;
        _monitor.Log($"Quest '{definitionId}' source override set to {source}.", LogLevel.Trace);
    }

    /// Returns the override (if set) or the definition's declared `Source`. Pipeline filters
    /// route off this rather than `def.Source` directly so an override re-targets the quest
    /// without re-registration.
    public TriggerSource EffectiveSource(IQuestDefinition def)
    {
        if (def == null) return TriggerSource.DailyBoard;
        return _sourceOverrides.TryGetValue(def.Id, out var s) ? s : def.Source;
    }

    /// Closes the registration window. Subsequent Register calls log and no-op. Intended to be
    /// invoked once content-pack and third-party registrations finish in Phase 5.
    public void Freeze() => _frozen = true;

    /// Test/debug helper. Resets the registry to empty so a fresh registration pass can run.
    public void Clear()
    {
        _byId.Clear();
        _ordered.Clear();
        _sourceOverrides.Clear();
        _frozen = false;
    }

    /// Snapshot for diagnostics. Allocates; not for hot paths.
    public IReadOnlyList<string> RegisteredIds() => _ordered.Select(d => d.Id).ToList();
}
