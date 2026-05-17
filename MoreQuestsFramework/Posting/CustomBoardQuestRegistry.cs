using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting;

// Names are namespaced as {ownerUniqueId}/{name}, mirroring CustomStepRegistry.
// A QuestPosting with QuestType == BoardQuestType.Custom carries the handler id in
// CustomQuestType; bare names resolve under the owning consumer mod, "OtherMod/Name"
// references another mod's handler.
internal sealed class CustomBoardQuestRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, Func<CustomBoardQuestContext, Quest?>> _handlers
        = new(StringComparer.OrdinalIgnoreCase);

    public CustomBoardQuestRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string ownerUniqueId, string name, Func<CustomBoardQuestContext, Quest?> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _monitor.Log($"CustomBoardQuestRegistry rejected registration from '{ownerUniqueId}': name is empty.", LogLevel.Warn);
            return;
        }
        if (handler == null)
        {
            _monitor.Log($"CustomBoardQuestRegistry rejected '{name}' from '{ownerUniqueId}': handler is null.", LogLevel.Warn);
            return;
        }
        string fq = Qualify(ownerUniqueId, name);
        if (_handlers.ContainsKey(fq))
        {
            _monitor.Log($"CustomBoardQuestRegistry rejected duplicate handler '{fq}'.", LogLevel.Warn);
            return;
        }
        _handlers[fq] = handler;
    }

    public Func<CustomBoardQuestContext, Quest?>? Resolve(string ownerUniqueId, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (_handlers.TryGetValue(name, out var direct))
            return direct;
        string fq = Qualify(ownerUniqueId, name);
        return _handlers.TryGetValue(fq, out var scoped) ? scoped : null;
    }

    private static string Qualify(string ownerUniqueId, string name)
        => name.Contains('/') ? name : $"{ownerUniqueId}/{name}";
}
