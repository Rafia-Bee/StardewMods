using System.Collections.Generic;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

// Reads the Notices content asset into the NoticeRegistry. Mirrors BoardPackLoader: infer
// the owner from the id when it isn't set, name the def after its key, register.
internal sealed class NoticePackLoader
{
    private readonly NoticeRegistry _registry;
    private readonly IMonitor _monitor;

    public NoticePackLoader(NoticeRegistry registry, IMonitor monitor)
    {
        _registry = registry;
        _monitor = monitor;
    }

    public void LoadFromAsset(IDictionary<string, NoticeDef> entries)
    {
        int registered = 0;
        foreach (var (id, def) in entries)
        {
            if (string.IsNullOrWhiteSpace(id) || def == null)
            {
                _monitor.Log($"Skipping empty notice entry (id='{id}').", LogLevel.Warn);
                continue;
            }

            string owner;
            if (!string.IsNullOrWhiteSpace(def.Owner))
                owner = def.Owner!;
            else
            {
                owner = InferOwner(id);
                if (owner == id && !id.Contains('.') && !id.Contains('_'))
                    _monitor.Log($"Notice id '{id}' has no namespace separator and no explicit Owner. Two packs using this id will collide. Use '{{{{ModId}}}}_Name' in your CP pack or set Owner.", LogLevel.Warn);
            }

            def.Name = id;
            _registry.Register(def, owner);
            registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{entries.Count} notices from CP asset.");
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
