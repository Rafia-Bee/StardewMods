using System;
using System.IO;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

public sealed class BoardPackLoader
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
        _monitor.Log($"Loaded {registered}/{doc.Boards.Count} boards from '{ownerUniqueId}'.", LogLevel.Trace);
    }
}
