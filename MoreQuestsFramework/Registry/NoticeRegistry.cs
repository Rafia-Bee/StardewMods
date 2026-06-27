using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using StardewModdingAPI;

namespace MoreQuestsFramework.Registry;

// Holds bulletin notices, keyed by {ownerUniqueId}/{Name}. Mirrors BoardRegistry: register
// during the open window, freeze for the session, clear on a hot reload.
internal sealed class NoticeRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, NoticeDef> _byKey
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NoticeDef> _ordered = new();
    private bool _frozen;

    public NoticeRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public IReadOnlyList<NoticeDef> All => _ordered;

    public void Register(NoticeDef def, string ownerUniqueId)
    {
        if (_frozen)
        {
            _monitor.Log($"NoticeRegistry rejected '{def.Name}': registry is frozen for this session.", LogLevel.Warn);
            return;
        }
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            _monitor.Log($"NoticeRegistry rejected notice from '{ownerUniqueId}': missing 'Name'.", LogLevel.Warn);
            return;
        }
        if (string.IsNullOrWhiteSpace(def.Title) && string.IsNullOrWhiteSpace(def.Body))
        {
            _monitor.Log($"NoticeRegistry rejected '{ownerUniqueId}/{def.Name}': a notice needs at least a 'Title' or a 'Body'.", LogLevel.Warn);
            return;
        }

        string key = ownerUniqueId + "/" + def.Name;
        if (_byKey.ContainsKey(key))
        {
            _monitor.Log($"NoticeRegistry rejected duplicate registration for '{key}'.", LogLevel.Warn);
            return;
        }
        def.OwnerUniqueId = ownerUniqueId;
        _byKey[key] = def;
        _ordered.Add(def);
        ModEntry.LogDebug($"Registered notice '{key}'.");
    }

    // Persistence keys are namespaced {owner}/{Name}, since two mods may use the same notice
    // Name. Used by FrameworkState.PruneDeadNoticeIds to drop a removed mod's saved flags.
    public static string KeyOf(NoticeDef def) => (def.OwnerUniqueId ?? "") + "/" + (def.Name ?? "");

    public IReadOnlyCollection<string> RegisteredIds()
    {
        var ids = new List<string>(_ordered.Count);
        foreach (var def in _ordered)
            if (!string.IsNullOrEmpty(def.Name))
                ids.Add(KeyOf(def));
        return ids;
    }

    public void Freeze() => _frozen = true;

    public void Clear()
    {
        _byKey.Clear();
        _ordered.Clear();
        _frozen = false;
    }
}
