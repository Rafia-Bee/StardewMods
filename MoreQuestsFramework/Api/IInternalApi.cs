using System;
using StardewModdingAPI;

namespace MoreQuestsFramework.Api;

/// Internal-only API exposed via SMAPI's `Mod.GetApi()` so consumer mods (currently just
/// our own `RafiaBee.MoreQuests` content mod) can register quests + custom Quest subclasses
/// with the framework. This shape will be replaced by the public `IMoreQuestsApi` in Phase 5
/// (§4 of plan.md); keeping it minimal until then so we don't lock ourselves into a surface.
public interface IInternalApi
{
    /// Adds a quest definition to the registry. Duplicate IDs are logged and rejected.
    void RegisterQuest(IQuestDefinition definition);

    /// Registers a custom `Quest` subclass with SpaceCore's serializer factory so it
    /// survives a save/load round-trip. Wraps SpaceCore so consumer mods don't need
    /// their own SpaceCore reference.
    void RegisterCustomQuestType(Type questType);

    /// Registers a named generator that JSON quests can reference via
    /// `"Generator": "<name>"`. Generators are namespaced by the calling mod's
    /// UniqueID; an unqualified reference in JSON resolves against the JSON's
    /// owning mod first, then falls back to a literal `OtherMod/Name` form.
    void RegisterGenerator(IManifest owner, string name, Func<QuestContext, QuestPosting?> generator);

    /// Reads a `quests.json` from a SMAPI content pack and registers each entry.
    /// Errors are logged with the offending mod + quest name so authors can
    /// locate problems quickly. (plan.md §5.1)
    void LoadContentPack(IContentPack pack);

    /// Reads a `quests.json` bundled inside a regular C# mod (relative to the
    /// mod folder) and registers each entry. Used by our own `RafiaBee.MoreQuests`
    /// content mod which ships `assets/quests.json` alongside C# generators.
    void LoadQuestsFromMod(IModHelper helper, IManifest manifest, string relativePath);

    /// Re-rolls today's daily-board postings. Test/debug aid that lets the user
    /// see new quest variants without reloading the save (plan.md §4).
    void RefreshOffers();
}
