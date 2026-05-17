using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoreQuestsFramework.Rewards;

// Names are namespaced as {ownerUniqueId}/{name}, mirroring GeneratorRegistry. A
// CustomReward's Kind field is the handler id (literal "OtherMod/Name" works for
// cross-mod references, bare names resolve under the registering mod's UniqueID).
internal sealed class CustomRewardRegistry
{
    public delegate void ApplyDelegate(string payload);
    public delegate string SummarizeDelegate(string payload, string questGiver, ITranslationHelper translation);

    private readonly IMonitor _monitor;
    private readonly Dictionary<string, Entry> _handlers
        = new(StringComparer.OrdinalIgnoreCase);

    public CustomRewardRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string ownerUniqueId, string name, ApplyDelegate apply, SummarizeDelegate? summarize)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _monitor.Log($"CustomRewardRegistry rejected registration from '{ownerUniqueId}': name is empty.", LogLevel.Warn);
            return;
        }
        if (apply == null)
        {
            _monitor.Log($"CustomRewardRegistry rejected '{name}' from '{ownerUniqueId}': apply is null.", LogLevel.Warn);
            return;
        }
        string fq = Qualify(ownerUniqueId, name);
        if (_handlers.ContainsKey(fq))
        {
            _monitor.Log($"CustomRewardRegistry rejected duplicate handler '{fq}'.", LogLevel.Warn);
            return;
        }
        _handlers[fq] = new Entry(apply, summarize);
    }

    public Entry? Resolve(string kind)
    {
        if (string.IsNullOrEmpty(kind))
            return null;
        return _handlers.TryGetValue(kind, out var entry) ? entry : null;
    }

    private static string Qualify(string ownerUniqueId, string name)
        => name.Contains('/') ? name : $"{ownerUniqueId}/{name}";

    public sealed class Entry
    {
        public ApplyDelegate Apply { get; }
        public SummarizeDelegate? Summarize { get; }

        public Entry(ApplyDelegate apply, SummarizeDelegate? summarize)
        {
            Apply = apply;
            Summarize = summarize;
        }
    }
}
