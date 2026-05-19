using System;
using System.Collections.Generic;
using System.IO;
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

    public void LoadContentPack(IContentPack pack)
    {
        BoardPackDocument? doc;
        try
        {
            doc = pack.ReadJsonFile<BoardPackDocument>("boards.json");
        }
        catch (Exception ex)
        {
            _monitor.Log($"Content pack '{pack.Manifest.UniqueID}': failed to read boards.json — {ex.Message}", LogLevel.Error);
            return;
        }
        if (doc == null)
            return;

        Apply(doc, pack.Manifest.UniqueID);
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

            if (!id.Contains('.') && !id.Contains('_'))
                _monitor.Log($"Board id '{id}' has no namespace separator. Two packs using this id will collide. Use '{{{{ModId}}}}_Name' in your CP pack.", LogLevel.Warn);

            def.Name = id;
            _registry.Register(def, id);
            registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{entries.Count} boards from CP asset.");
    }

    public void LoadFromMod(IModHelper helper, IManifest manifest, string relativePath)
    {
        BoardPackDocument? doc;
        try
        {
            doc = helper.Data.ReadJsonFile<BoardPackDocument>(relativePath);
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
        Apply(doc, manifest.UniqueID);
    }

    private void Apply(BoardPackDocument doc, string ownerUniqueId)
    {
        if (!string.Equals(doc.Schema, "1.0", StringComparison.OrdinalIgnoreCase))
        {
            _monitor.Log($"'{ownerUniqueId}': unknown boards.json Schema '{doc.Schema}'. Expected '1.0'. Continuing.", LogLevel.Warn);
        }

        int registered = 0;
        foreach (var def in doc.Boards)
        {
            _registry.Register(def, ownerUniqueId);
            registered++;
        }
        ModEntry.LogDebug($"Loaded {registered}/{doc.Boards.Count} boards from '{ownerUniqueId}'.");
    }
}
