using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Registry;

internal sealed class QuestRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, IQuestDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IQuestDefinition> _ordered = new();
    private readonly Dictionary<string, TriggerSource> _sourceOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TriggerSource> _pendingOverrides = new(StringComparer.OrdinalIgnoreCase);
    private bool _frozen;

    public QuestRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public IReadOnlyList<IQuestDefinition> All => _ordered;

    public IEnumerable<IQuestDefinition> WithKind(PostingKind kind)
    {
        for (int i = 0; i < _ordered.Count; i++)
        {
            if (_ordered[i].Kind == kind)
                yield return _ordered[i];
        }
    }

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

        // Drain any buffered override that targeted this id before its owner mod ran.
        if (_pendingOverrides.TryGetValue(def.Id, out var pending))
        {
            _sourceOverrides[def.Id] = pending;
            _pendingOverrides.Remove(def.Id);
            _monitor.Log($"Applied buffered source override on '{def.Id}': {pending}.", LogLevel.Trace);
        }
    }

    public bool TryGet(string id, out IQuestDefinition? def) => _byId.TryGetValue(id, out def!);

    // Allowed both during the registration window and after freeze. A removed quest
    // stops appearing in pipeline draws on the next DayStarted, in-flight quests
    // already in the player's journal keep working (their state lives on the Quest
    // instance, not the registry).
    public bool Unregister(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return false;
        if (!_byId.TryGetValue(definitionId, out var def))
        {
            _monitor.Log($"Unregister('{definitionId}') ignored: no such quest registered.", LogLevel.Warn);
            return false;
        }
        _byId.Remove(definitionId);
        _ordered.Remove(def);
        _sourceOverrides.Remove(definitionId);
        _pendingOverrides.Remove(definitionId);
        _monitor.Log($"Quest '{definitionId}' unregistered.", LogLevel.Trace);
        return true;
    }

    // Allowed both during the registration window and after freeze (metadata only,
    // doesn't add or remove definitions). Calls during the registration window that
    // target an id whose owner mod hasn't called RegisterQuest yet are buffered and
    // replayed when Register sees the matching id. Buffered overrides that never
    // resolve get a Warn at Freeze time.
    public void OverrideSource(string definitionId, TriggerSource source)
    {
        if (string.IsNullOrEmpty(definitionId))
            return;
        if (_byId.ContainsKey(definitionId))
        {
            _sourceOverrides[definitionId] = source;
            _monitor.Log($"Quest '{definitionId}' source override set to {source}.", LogLevel.Trace);
            return;
        }
        if (_frozen)
        {
            _monitor.Log($"OverrideSource('{definitionId}', {source}) ignored: no such quest registered.", LogLevel.Warn);
            return;
        }
        _pendingOverrides[definitionId] = source;
        _monitor.Log($"Buffered source override on '{definitionId}' = {source} until its owner registers.", LogLevel.Trace);
    }

    public TriggerSource EffectiveSource(IQuestDefinition def)
    {
        if (def == null) return TriggerSource.DailyBoard;
        return _sourceOverrides.TryGetValue(def.Id, out var s) ? s : def.Source;
    }

    public void Freeze()
    {
        _frozen = true;
        if (_pendingOverrides.Count > 0)
        {
            foreach (var (id, source) in _pendingOverrides)
                _monitor.Log($"Buffered OverrideSource for '{id}' = {source} never resolved: no quest with that id was registered.", LogLevel.Warn);
            _pendingOverrides.Clear();
        }
    }

    public void Clear()
    {
        _byId.Clear();
        _ordered.Clear();
        _sourceOverrides.Clear();
        _pendingOverrides.Clear();
        _frozen = false;
    }

    public IReadOnlyList<string> RegisteredIds() => _ordered.Select(d => d.Id).ToList();
}
