using System;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Triggers;
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

    /// Variant of `LoadContentPack` that wires a cooldown-tier resolver into every quest the
    /// pack registers. The resolver is called at trigger evaluation time with the quest's
    /// `Trigger.CooldownTier` string and should return the in-game day count for that tier.
    /// Returning null falls back to the JSON's `CooldownDays` literal. Lets a content mod
    /// surface a small set of shared cooldown buckets in GMCM and have edits apply live.
    void LoadContentPack(IContentPack pack, Func<string, int?> cooldownTierResolver);

    /// Reads a `quests.json` bundled inside this mod's folder (relative to the mod
    /// directory) and registers each entry.
    void LoadQuestsFromMod(IModHelper helper, string relativePath);

    /// Variant of `LoadQuestsFromMod` that wires a cooldown-tier resolver. See the
    /// `LoadContentPack` overload above for the resolver contract.
    void LoadQuestsFromMod(IModHelper helper, string relativePath, Func<string, int?> cooldownTierResolver);

    /// Adds an NPC to the named dispatch role. Optional `requiredModUniqueId` filters
    /// the entry out unless that mod is loaded — used to scope modded NPCs to their
    /// host mod. Authors can add new roles by passing any string they like.
    void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null);

    /// Registers a custom pin-board placed at a tile in a named location. The framework
    /// renders the board sprite (if `Texture` is set), gates visibility on `Available`,
    /// and opens a `CustomBoardMenu` when the player presses the action button on the
    /// anchor tile. Phase 8c routes `TriggerSource.CustomBoard` quests to the matching
    /// board's slot list; until then the board opens with vanilla's "Nothing posted"
    /// fallback string.
    void RegisterBoard(BoardDefinition board);

    /// Reads a `boards.json` bundled inside this mod's folder (relative to the mod
    /// directory) and registers each entry. Mirrors `LoadQuestsFromMod` for boards.
    void LoadBoardsFromMod(IModHelper helper, string relativePath);

    /// Returns the board this mod registered under the given short `name`, or null. The
    /// returned `BoardDefinition` is the live registry instance, so mutating its fields
    /// (e.g. `Tile`, `DrawOffset`) takes effect on the next render. Useful for content
    /// mods that want to drive their board's draw position from a config menu.
    BoardDefinition? FindBoard(string name);

    /// Re-routes an already-registered quest to a different `TriggerSource`. The override
    /// is consulted by the pipeline instead of the definition's declared `Source`, so a
    /// quest authored as `CustomBoard` can be flipped to `DailyBoard` (or vice versa) at
    /// runtime without re-registering. Useful for player-toggleable routing — e.g. a
    /// content mod's "enable Adventurer's Guild board" config flag flipping the guild's
    /// quest pool back to the help-wanted board when off so the content stays reachable.
    /// No-ops with a warning if no quest with that id is registered.
    void OverrideTriggerSource(string definitionId, TriggerSource source);

    /// Replaces the built-in handler for `tier` with the supplied implementation. Useful
    /// for content packs that want a different friendship-delta curve, dialogue-queue
    /// cadence, or gold-loss formula than the framework defaults. The framework's seed
    /// handlers are registered before `RegistrationOpen` fires, so a consumer-mod call
    /// during registration cleanly takes precedence. Engine availability follows the
    /// save lifecycle — calls before the first save load are buffered and applied to
    /// each fresh engine when it stands up.
    void RegisterConsequenceTier(ConsequenceTier tier, IConsequenceHandler handler);
}
