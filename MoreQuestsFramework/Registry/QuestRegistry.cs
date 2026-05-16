using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Registry;

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
    }

    public bool TryGet(string id, out IQuestDefinition? def) => _byId.TryGetValue(id, out def!);

    // Allowed both during the registration window and after freeze (metadata only,
    // doesn't add or remove definitions).
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

    public TriggerSource EffectiveSource(IQuestDefinition def)
    {
        if (def == null) return TriggerSource.DailyBoard;
        return _sourceOverrides.TryGetValue(def.Id, out var s) ? s : def.Source;
    }

    public void Freeze() => _frozen = true;

    public void Clear()
    {
        _byId.Clear();
        _ordered.Clear();
        _sourceOverrides.Clear();
        _frozen = false;
    }

    public IReadOnlyList<string> RegisteredIds() => _ordered.Select(d => d.Id).ToList();
}
