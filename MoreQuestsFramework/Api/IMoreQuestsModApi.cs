using System;
using StardewModdingAPI;

namespace MoreQuestsFramework.Api;

/// Per-consumer-mod scope. Every registration is namespaced by the calling mod's
/// UniqueID, so two mods can ship a quest or generator with the same short name
/// without colliding. Obtained via `IMoreQuestsApi.GetModApi(ModManifest)`.
public interface IMoreQuestsModApi
{
    /// The manifest that owns this scope. Convenience accessor for handlers that
    /// need to log "who's calling".
    IManifest Owner { get; }

    /// Adds a quest definition to the registry. Duplicate IDs are logged and rejected.
    void RegisterQuest(IQuestDefinition definition);

    /// Registers a custom `Quest` subclass with SpaceCore's serializer factory so
    /// it survives a save/load round-trip. Wraps SpaceCore so consumer mods don't
    /// need their own SpaceCore reference.
    void RegisterCustomQuestType(Type questType);

    /// Registers a named C# generator that JSON quests can reference via
    /// `"Generator": "<name>"`. Names are namespaced as `{ownerUniqueId}/{name}`.
    void RegisterGenerator(string name, Func<QuestContext, QuestPosting?> generator);

    /// Reads a `quests.json` from a SMAPI content pack and registers each entry.
    void LoadContentPack(IContentPack pack);

    /// Reads a `quests.json` bundled inside this mod's folder (relative to the mod
    /// directory) and registers each entry.
    void LoadQuestsFromMod(IModHelper helper, string relativePath);

    /// Adds an NPC to the named dispatch role. Optional `requiredModUniqueId` filters
    /// the entry out unless that mod is loaded — used to scope modded NPCs to their
    /// host mod. Authors can add new roles by passing any string they like.
    void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null);
}
