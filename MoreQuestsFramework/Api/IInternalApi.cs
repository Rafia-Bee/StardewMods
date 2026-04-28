using System;

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
}
