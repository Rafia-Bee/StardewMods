using System;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Api;

// Per-consumer-mod scope. Every registration is namespaced by the calling mod's
// UniqueID. Obtained via IMoreQuestsApi.GetModApi(ModManifest).
public interface IMoreQuestsModApi
{
    IManifest Owner { get; }

    void RegisterQuest(IQuestDefinition definition);

    // Registers a custom Quest subclass with SpaceCore's serializer factory so it
    // survives save/load. Wraps SpaceCore so consumers don't need their own reference.
    void RegisterCustomQuestType(Type questType);

    // Named C# generator JSON quests can reference via "Generator": "<name>".
    // Names are namespaced as {ownerUniqueId}/{name}.
    void RegisterGenerator(string name, Func<QuestContext, QuestPosting?> generator);

    void LoadContentPack(IContentPack pack);

    // Resolver is called at trigger time with the quest's Trigger.CooldownTier string
    // and should return an in-game day count (or null to fall back to JSON's CooldownDays).
    void LoadContentPack(IContentPack pack, Func<string, int?> cooldownTierResolver);

    void LoadQuestsFromMod(IModHelper helper, string relativePath);

    void LoadQuestsFromMod(IModHelper helper, string relativePath, Func<string, int?> cooldownTierResolver);

    // Optional requiredModUniqueId filters the entry out unless that mod is loaded.
    void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null);

    void RegisterBoard(BoardDefinition board);

    void LoadBoardsFromMod(IModHelper helper, string relativePath);

    // Returned BoardDefinition is the live registry instance; mutating fields like
    // Tile/DrawOffset takes effect on the next render.
    BoardDefinition? FindBoard(string name);

    // Re-routes an already-registered quest to a different TriggerSource at runtime.
    // No-ops with a warning if no quest with that id is registered.
    void OverrideTriggerSource(string definitionId, TriggerSource source);

    // Calls before the first save load are buffered and applied to each fresh engine
    // when it stands up.
    void RegisterConsequenceTier(ConsequenceTier tier, IConsequenceHandler handler);
}
