using System.Collections.Generic;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

internal sealed class BoardPackLoader
{
    private readonly BoardRegistry _registry;
    private readonly IMonitor _monitor;

    public BoardPackLoader(BoardRegistry registry, IMonitor monitor)
    {
        _registry = registry;
        _monitor = monitor;
    }

    public void LoadFromAsset(IDictionary<string, BoardDefinition> entries)
    {
        int registered = 0;
        foreach (var (id, def) in entries)
        {
            if (string.IsNullOrWhiteSpace(id) || def == null)
            {
                _monitor.Log($"Skipping empty board entry (id='{id}').", LogLevel.Warn);
                continue;
            }

            string owner;
            if (!string.IsNullOrWhiteSpace(def.OwnerUniqueId))
                owner = def.OwnerUniqueId;
            else
            {
                owner = InferOwner(id);
                if (owner == id && !id.Contains('.') && !id.Contains('_'))
                    _monitor.Log($"Board id '{id}' has no namespace separator and no explicit OwnerUniqueId. Two packs using this id will collide. Use '{{{{ModId}}}}_Name' in your CP pack or set OwnerUniqueId.", LogLevel.Warn);
            }

            def.Name = id;
            _registry.Register(def, owner);
            registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{entries.Count} boards from CP asset.");
    }

    private static string InferOwner(string id)
    {
        int underscore = id.IndexOf('_');
        if (underscore > 0)
        {
            string prefix = id.Substring(0, underscore);
            if (prefix.Contains('.'))
                return prefix;
        }
        return id;
    }
}
