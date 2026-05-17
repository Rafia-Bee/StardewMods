using System;
using System.Collections.Generic;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Per-consumer-mod scope. Every registration is namespaced by the calling mod's
// UniqueID. Obtained via IMoreQuestsApi.GetModApi(ModManifest).
public interface IMoreQuestsModApi
{
    IManifest Owner { get; }

    // Returns true on success. False means the registration was rejected (duplicate id
    // or the registration window has already closed); the reason is logged at Warn.
    bool RegisterQuest(IQuestDefinition definition);

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

    // Removes a quest definition from the registry. Allowed both before and after
    // freeze. Quests already in the player's journal keep working; the def just
    // stops being a draw candidate on the next DayStarted. No-ops with a warning
    // if no quest with that id is registered. The id is global, any mod can
    // unregister any quest (the same posture as OverrideTriggerSource).
    void Unregister(string definitionId);

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

    // Event-driven escape hatch for Custom steps. Returns a snapshot of every
    // active Custom step whose Targets[0] resolves to the given handler name (bare
    // names are scoped to the calling mod, "OtherMod/Name" works for cross-mod
    // refs). Call from a Harmony patch or SMAPI event handler and invoke
    // AddProgress / MarkDone on the returned handle(s). Handles are short-lived,
    // re-query each event tick rather than caching them. Skips registration entirely:
    // a step can be advanced this way without ever calling RegisterCustomAdventureStep,
    // useful when polling isn't a fit (e.g. credit one tick per "boss slain"
    // OnMonsterSlain event).
    IReadOnlyList<ICustomStepHandle> GetActiveCustomSteps(string handlerName);

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

    // Handler for a new condition key recognised inside the JSON `Available { ... }`
    // block (the same dictionary the built-in `Season` / `NpcMet` / etc. keys live
    // in). The evaluator gets the raw value string and returns true/false. Keys are
    // case-insensitive, OR alternatives ("|"), and the "not:" prefix all work
    // because they're applied above EvaluateOne. Keys must not collide with
    // built-ins or with another mod's registration (first registration wins, the
    // second is rejected with a Warn).
    void RegisterCustomCondition(string key, Func<string, bool> evaluator);

    // Handler for QuestPosting.QuestType == BoardQuestType.Custom. JSON quests
    // declare `"Objective": { "Kind": "Custom", "Custom": "<handler>" }`; the
    // framework calls the handler at posting time, takes the returned Quest
    // instance, and applies the usual title/description/reward wiring on top.
    // Bare names resolve under the calling mod's UniqueID; pass
    // "OtherMod.UniqueID/Name" for cross-mod references.
    void RegisterCustomBoardQuestType(string name, Func<CustomBoardQuestContext, Quest?> handler);

    // Lets the framework round-trip a custom Quest subclass through the mail-stash
    // DTO so a mail-delivered quest survives a save+reload before the player opens
    // the letter. `kind` is a stable string id stored alongside the stash, so don't
    // rename it after release. `encode` is called with the live Quest at post-time
    // and must return the subclass's variable state as a list of strings; `decode`
    // is called at SaveLoaded with the same list and must return a fresh Quest with
    // its NetFields populated. The framework re-applies title/description/daysLeft
    // and the standard reward+consequence wiring on top of whatever `decode` returns,
    // so codecs only have to cover their own extra fields. Quest subclasses with
    // no registered codec still post, but they log a Warn and vanish if the player
    // reloads before reading the letter.
    void RegisterMailStashCodec(
        string kind,
        Type questType,
        Func<Quest, IList<string>> encode,
        Func<IList<string>, Quest?> decode);
}
