using System;
using System.Collections.Generic;
using System.IO;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

/// Reads `quests.json` files and registers each entry with the framework registry as
/// a `JsonQuestDefinition`. Errors are logged with the offending mod + quest name so
/// authors can find the bad row quickly.
public sealed class QuestPackLoader
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

    /// Loads from a SMAPI `IContentPack`. The pack's translation helper resolves
    /// `{i18n:...}` tokens. Used by third-party content packs that declare
    /// `ContentPackFor: RafiaBee.MoreQuestsFramework`.
    public void LoadContentPack(IContentPack pack)
    {
        QuestPackDocument? doc;
        try
        {
            doc = pack.ReadJsonFile<QuestPackDocument>("quests.json");
        }
        catch (Exception ex)
        {
            _monitor.Log($"Content pack '{pack.Manifest.UniqueID}': failed to read quests.json — {ex.Message}", LogLevel.Error);
            return;
        }
        if (doc == null)
        {
            _monitor.Log($"Content pack '{pack.Manifest.UniqueID}': no quests.json found.", LogLevel.Warn);
            return;
        }
        Apply(doc, pack.Manifest.UniqueID, pack.Translation);
    }

    /// Loads from a JSON file bundled inside a regular C# mod's folder. Used by
    /// our own `RafiaBee.MoreQuests` content mod which ships `assets/quests.json`
    /// alongside generators registered in C#.
    public void LoadFromMod(IModHelper helper, IManifest manifest, string relativePath)
    {
        QuestPackDocument? doc;
        try
        {
            doc = helper.Data.ReadJsonFile<QuestPackDocument>(relativePath);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Mod '{manifest.UniqueID}': failed to read '{relativePath}' — {ex.Message}", LogLevel.Error);
            return;
        }
        if (doc == null)
        {
            string fullPath = Path.Combine(helper.DirectoryPath, relativePath);
            _monitor.Log($"Mod '{manifest.UniqueID}': '{relativePath}' not found at '{fullPath}'.", LogLevel.Warn);
            return;
        }
        Apply(doc, manifest.UniqueID, helper.Translation);
    }

    private void Apply(QuestPackDocument doc, string ownerUniqueId, ITranslationHelper translation)
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

            var jdef = new JsonQuestDefinition(def, ownerUniqueId, translation, _generators, _monitor);
            _registry.Register(jdef);
            registered++;
        }
        _monitor.Log($"Loaded {registered}/{doc.Quests.Count} quests from '{ownerUniqueId}'.", LogLevel.Trace);
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
        if (string.IsNullOrEmpty(def.Generator) && def.Objective == null)
        {
            _monitor.Log($"'{ownerUniqueId}/{label}': must set either 'Generator' or 'Objective'. Skipping.", LogLevel.Warn);
            return false;
        }
        if (!string.IsNullOrEmpty(def.Generator) && def.Objective != null)
        {
            _monitor.Log($"'{ownerUniqueId}/{label}': has both 'Generator' and 'Objective'; 'Objective' is ignored.", LogLevel.Warn);
        }
        if (def.Objective != null && string.IsNullOrEmpty(def.Objective.Item) && def.Objective.Kind?.ToLowerInvariant() != "slay")
        {
            _monitor.Log($"'{ownerUniqueId}/{label}': declarative Objective is missing 'Item'. Skipping.", LogLevel.Warn);
            return false;
        }
        return true;
    }
}
