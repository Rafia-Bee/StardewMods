using System;
using System.Collections.Generic;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

internal sealed class QuestPackLoader
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
    private readonly IMonitor _monitor;

    public QuestPackLoader(QuestRegistry registry, GeneratorRegistry generators, IMonitor monitor)
    {
        _registry = registry;
        _generators = generators;
        _monitor = monitor;
    }

    // cooldownTierLookup is expected to re-read its backing dict (the CooldownTiers
    // asset) on every call, so GMCM edits that invalidate the asset take effect
    // without a save reload. Pass null when the consumer mod doesn't use named tiers.
    public void LoadFromAsset(IDictionary<string, QuestDef> entries, Func<string, int?>? cooldownTierLookup)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int registered = 0;
        foreach (var (id, def) in entries)
        {
            if (string.IsNullOrWhiteSpace(id) || def == null)
            {
                _monitor.Log($"Skipping empty quest entry (id='{id}').", LogLevel.Warn);
                continue;
            }

            if (!id.Contains('.') && !id.Contains('_'))
                _monitor.Log($"Quest id '{id}' has no namespace separator. Two packs using this id will collide. Use '{{{{ModId}}}}_Name' in your CP pack.", LogLevel.Warn);

            def.Name = id;

            if (!Validate(def, id, seen))
                continue;

            string owner = !string.IsNullOrWhiteSpace(def.Owner) ? def.Owner! : InferOwner(id);
            var jdef = new JsonQuestDefinition(def, owner, _generators, _monitor, cooldownTierLookup);
            if (_registry.Register(jdef))
                registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{entries.Count} quests from CP asset.");
    }

    // Splits on the first '_'; the prefix is treated as a SMAPI UniqueID when it
    // contains a '.'. Matches the "{{ModId}}_Name" convention CP authors follow.
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

    private bool Validate(QuestDef def, string ownerUniqueId, HashSet<string> seen)
    {
        string label = def.Name ?? "(unnamed)";
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            _monitor.Log($"'{ownerUniqueId}': quest is missing 'Name'. Skipping.", LogLevel.Warn);
            return false;
        }
        if (!seen.Add(def.Name))
        {
            _monitor.Log($"'{ownerUniqueId}/{label}': duplicate quest name in this pack. Skipping.", LogLevel.Warn);
            return false;
        }
        bool hasSteps = def.Steps != null && def.Steps.Count > 0;
        if (string.IsNullOrEmpty(def.Generator) && def.Objective == null && !hasSteps)
        {
            _monitor.Log($"'{ownerUniqueId}/{label}': must set 'Generator', 'Objective', or 'Steps'. Skipping.", LogLevel.Warn);
            return false;
        }
        if (!string.IsNullOrEmpty(def.Generator) && (def.Objective != null || hasSteps))
        {
            _monitor.Log($"'{ownerUniqueId}/{label}': has 'Generator' alongside declarative fields; declarative fields are ignored.", LogLevel.Warn);
        }
        if (def.Objective != null && (def.Objective.Item == null || def.Objective.Item.Count == 0))
        {
            string kindLower = def.Objective.Kind?.ToLowerInvariant() ?? "";
            if (kindLower != "slay" && kindLower != "custom")
            {
                _monitor.Log($"'{ownerUniqueId}/{label}': declarative Objective is missing 'Item'. Skipping.", LogLevel.Warn);
                return false;
            }
        }
        if (def.Rewards != null)
        {
            foreach (var r in def.Rewards)
            {
                if (!IsKnownRewardKind(r?.Kind))
                {
                    _monitor.Log($"'{ownerUniqueId}/{label}': unknown reward Kind '{r?.Kind}'. Skipping the whole quest so a typo doesn't ship a quest with missing rewards.", LogLevel.Warn);
                    return false;
                }
            }
        }
        if (hasSteps)
        {
            foreach (var sd in def.Steps!)
            {
                if (!Enum.TryParse<AdventureStepKind>(sd?.Kind, ignoreCase: true, out _))
                {
                    _monitor.Log($"'{ownerUniqueId}/{label}': step '{sd?.Name}' has unknown Kind '{sd?.Kind}'. Skipping the whole quest so a typo doesn't ship a quest that can't be completed.", LogLevel.Warn);
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsKnownRewardKind(string? kind)
    {
        if (string.IsNullOrEmpty(kind))
            return false;
        switch (kind.ToLowerInvariant())
        {
            case "money":
            case "friendship":
            case "object":
            case "recipe":
            case "mail":
            case "shopdiscount":
            case "animalpurchasediscount":
            case "festivalbias":
            case "fairstartokens":
            case "custom":
                return true;
            default:
                return false;
        }
    }
}
