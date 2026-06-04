using System;
using System.Collections.Generic;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

// Names are namespaced as {ownerUniqueId}/{name}, mirroring CustomStepRegistry. A
// report-back Custom step's Targets[0] is the prompt id (literal "OtherMod/Name" or a
// bare name resolved against the owning consumer mod's UniqueID).
internal sealed class ReportBackRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, ReportBackPrompt> _prompts
        = new(StringComparer.OrdinalIgnoreCase);

    public ReportBackRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public bool IsEmpty => _prompts.Count == 0;

    public void Register(string ownerUniqueId, string name, ReportBackPrompt prompt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _monitor.Log($"ReportBackRegistry rejected registration from '{ownerUniqueId}': name is empty.", LogLevel.Warn);
            return;
        }
        if (prompt == null || prompt.Options == null || prompt.Options.Count == 0)
        {
            _monitor.Log($"ReportBackRegistry rejected '{name}' from '{ownerUniqueId}': prompt has no options.", LogLevel.Warn);
            return;
        }
        string fq = Qualify(ownerUniqueId, name);
        if (_prompts.ContainsKey(fq))
        {
            _monitor.Log($"ReportBackRegistry rejected duplicate prompt '{fq}'.", LogLevel.Warn);
            return;
        }
        _prompts[fq] = prompt;
    }

    public ReportBackPrompt? Resolve(string ownerUniqueId, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (_prompts.TryGetValue(name, out var direct))
            return direct;
        if (!string.IsNullOrEmpty(ownerUniqueId)
            && _prompts.TryGetValue(Qualify(ownerUniqueId, name), out var scoped))
            return scoped;

        // The quest's owner can come back empty when its modData tracking is missing
        // (e.g. an odd reload path). Fall back to matching the bare name against any
        // registered prompt's suffix so the report-back still resolves.
        if (!name.Contains('/'))
        {
            string suffix = "/" + name;
            foreach (var (key, prompt) in _prompts)
                if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return prompt;
        }
        return null;
    }

    private static string Qualify(string ownerUniqueId, string name)
        => name.Contains('/') ? name : $"{ownerUniqueId}/{name}";
}
