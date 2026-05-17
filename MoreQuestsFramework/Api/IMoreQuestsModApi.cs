using System;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
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

    // Handler for AdventureStepKind.Custom steps. The framework polls each active
    // Custom step once per second, calls the handler with the step's context, and
    // credits the returned int against step.Progress (returning 0 = no progress this
    // tick, returning >= remaining marks the step Done). The step's Targets[0]
    // carries the handler id; bare names are looked up under the owning consumer
    // mod's scope, "OtherMod/Name" works for cross-mod references.
    void RegisterCustomAdventureStep(string name, Func<CustomStepContext, int> handler);

    // Handler for TriggerSource.Custom quests. The framework checks the definition's
    // CooldownDays first, then calls the handler at DayStarted to decide whether the
    // trigger fires today. The quest's Trigger.Custom field carries the handler id;
    // bare names are looked up under the owning consumer mod's scope, "OtherMod/Name"
    // works for cross-mod references.
    void RegisterCustomTrigger(string name, Func<CustomTriggerContext, bool> handler);

    // Handler for CustomReward kinds. Apply is called at questComplete with the raw
    // payload string. Summarize (optional) is called when the billboard/journal
    // renders the reward preview; return an empty string to skip the line. JSON
    // quests refer to the handler by `Custom: "<name>"` plus an optional `Payload`
    // string; bare names are scoped to the calling mod, "OtherMod/Name" works for
    // cross-mod references.
    void RegisterCustomReward(
        string name,
        Action<string> apply,
        Func<string, string, ITranslationHelper, string>? summarize = null);
}
