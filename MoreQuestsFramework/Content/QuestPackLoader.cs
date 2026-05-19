using System;
using System.Collections.Generic;
using System.IO;
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

    public void LoadContentPack(IContentPack pack, Func<string, int?>? cooldownTierResolver = null)
    {
        QuestPackDocument? doc;
        try
        {
            doc = pack.ReadJsonFile<QuestPackDocument>("quests.json");
        }
        catch (Exception ex)
        {
            _monitor.Log($"Content pack '{pack.Manifest.UniqueID}': failed to read quests.json, {ex.Message}", LogLevel.Error);
            return;
        }
        if (doc == null)
        {
            _monitor.Log($"Content pack '{pack.Manifest.UniqueID}': no quests.json found.", LogLevel.Warn);
            return;
        }
        Apply(doc, pack.Manifest.UniqueID, pack.Translation, cooldownTierResolver);
    }

    public void LoadFromAsset(IDictionary<string, QuestDef> entries, IDictionary<string, int> cooldownTiers, ITranslationHelper translation)
    {
        Func<string, int?>? resolver = cooldownTiers.Count > 0
            ? name => cooldownTiers.TryGetValue(name, out var days) ? days : null
            : null;

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

            var jdef = new JsonQuestDefinition(def, id, translation, _generators, _monitor, resolver);
            if (_registry.Register(jdef))
                registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{entries.Count} quests from CP asset.");
    }

    public void LoadFromMod(IModHelper helper, IManifest manifest, string relativePath, Func<string, int?>? cooldownTierResolver = null)
    {
        QuestPackDocument? doc;
        try
        {
            doc = helper.Data.ReadJsonFile<QuestPackDocument>(relativePath);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Mod '{manifest.UniqueID}': failed to read '{relativePath}', {ex.Message}", LogLevel.Error);
            return;
        }
        if (doc == null)
        {
            string fullPath = Path.Combine(helper.DirectoryPath, relativePath);
            _monitor.Log($"Mod '{manifest.UniqueID}': '{relativePath}' not found at '{fullPath}'.", LogLevel.Warn);
            return;
        }
        Apply(doc, manifest.UniqueID, helper.Translation, cooldownTierResolver);
    }

    private void Apply(QuestPackDocument doc, string ownerUniqueId, ITranslationHelper translation, Func<string, int?>? cooldownTierResolver)
    {
        if (!string.Equals(doc.Schema, "1.0", StringComparison.OrdinalIgnoreCase))
        {
            _monitor.Log($"'{ownerUniqueId}': unknown quests.json Schema '{doc.Schema}'. Expected '1.0'. Continuing.", LogLevel.Warn);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int registered = 0;
        foreach (var def in doc.Quests)
        {
            if (!Validate(def, ownerUniqueId, seen))
                continue;

            var jdef = new JsonQuestDefinition(def, ownerUniqueId, translation, _generators, _monitor, cooldownTierResolver);
            if (_registry.Register(jdef))
                registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{doc.Quests.Count} quests from '{ownerUniqueId}'.");
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
