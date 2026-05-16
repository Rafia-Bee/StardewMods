using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using StardewModdingAPI;

namespace MoreQuestsFramework.Registry;

public sealed class BoardRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, BoardDefinition> _byKey
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BoardDefinition> _ordered = new();
    private bool _frozen;

    public BoardRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public IReadOnlyList<BoardDefinition> All => _ordered;

    // Lookup key is namespaced as {ownerUniqueId}/{Name}.
    public void Register(BoardDefinition def, string ownerUniqueId)
    {
        if (_frozen)
        {
            _monitor.Log($"BoardRegistry rejected '{def.Name}': registry is frozen for this session.", LogLevel.Warn);
            return;
        }
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            _monitor.Log($"BoardRegistry rejected board from '{ownerUniqueId}': missing 'Name'.", LogLevel.Warn);
            return;
        }
        if (string.IsNullOrWhiteSpace(def.Location))
        {
            _monitor.Log($"BoardRegistry rejected '{ownerUniqueId}/{def.Name}': missing 'Location'.", LogLevel.Warn);
            return;
        }
        if (def.Tile == null || def.Tile.Length < 2)
        {
            _monitor.Log($"BoardRegistry rejected '{ownerUniqueId}/{def.Name}': 'Tile' must be a [x, y] pair.", LogLevel.Warn);
            return;
        }

        string key = ownerUniqueId + "/" + def.Name;
        if (_byKey.ContainsKey(key))
        {
            _monitor.Log($"BoardRegistry rejected duplicate registration for '{key}'.", LogLevel.Warn);
            return;
        }
        def.OwnerUniqueId = ownerUniqueId;
        _byKey[key] = def;
        _ordered.Add(def);
        _monitor.Log($"Registered board '{key}' at {def.Location} ({def.TileX}, {def.TileY}).", LogLevel.Trace);
    }

    public BoardDefinition? Find(string ownerUniqueId, string name)
    {
        string key = ownerUniqueId + "/" + name;
        return _byKey.TryGetValue(key, out var def) ? def : null;
    }

    // First match wins on Name collision across owners. Quest definitions look up by
    // OwnerUniqueId/Name so ambiguity there is impossible.
    public BoardDefinition? FindByName(string name)
    {
        for (int i = 0; i < _ordered.Count; i++)
            if (string.Equals(_ordered[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return _ordered[i];
        return null;
    }

    public IEnumerable<BoardDefinition> InLocation(string locationName)
    {
        for (int i = 0; i < _ordered.Count; i++)
            if (string.Equals(_ordered[i].Location, locationName, StringComparison.Ordinal))
                yield return _ordered[i];
    }

    public void Freeze() => _frozen = true;

    public void Clear()
    {
        _byKey.Clear();
        _ordered.Clear();
        _frozen = false;
    }
}
