using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Lifecycle:
//   1. SMAPI fires GameLaunched for every mod in dependency order.
//   2. On the framework's first UpdateTicking (one tick past its own GameLaunched, so every
//      consumer mod has had a chance to subscribe), RegistrationOpen fires. All Open
//      handlers run synchronously in subscription order during this call, then the
//      framework auto-loads owned content packs.
//   3. RegistrationClosed fires immediately after, in the same tick. The registry is
//      frozen at this point, so this is the right event for "I want to inspect what
//      everyone else registered" (e.g. building a quest browser or debug menu).
// Pick Open if you're registering things, Closed if you're reading them.
public interface IMoreQuestsApi
{
    IMoreQuestsModApi GetModApi(IManifest mod);

    bool IsManagedQuest(Quest quest);

    // For an item-delivery managed quest, returns the quality tier of the item the
    // player turned in. Vanilla quality ladder: 0 regular, 1 silver, 2 gold, 4 iridium.
    // Captured at delivery time, so it's stable inside a QuestCompleted handler even
    // though the player's inventory has already shed the stack. Returns null when the
    // quest isn't a framework item-delivery quest (e.g. a vanilla quest, a fishing
    // quest, an AdventureQuest, etc.).
    int? GetDeliveredQuality(Quest quest);

    // For a managed quest currently posted or in the player's log, returns the
    // registered definition id. Null when the quest isn't framework-managed. Useful
    // for content mods that want to identify a quest by id rather than by translated
    // title (which can shift across i18n updates or translation packs).
    string? GetDefinitionId(Quest quest);

    // Snapshot of every registered quest id in registration order. Consumer-mod
    // debug menus and quest browsers can pair this with GetQuestInfo to build
    // their own listings. Returns an empty list before RegistrationOpen fires.
    IReadOnlyList<string> RegisteredQuestIds();

    // Read-only metadata for a registered quest. Null when the id is unknown.
    QuestInfo? GetQuestInfo(string definitionId);

    // Dry-run the quest's IsAvailable(ctx) against the current save's context, useful
    // for debug menus or quest browsers. Returns null when the id is unknown or no save
    // is loaded. Note: built-in JSON conditions still read live Game1.* state today, so
    // the result reflects the running game, not a hypothetical scenario.
    bool? IsQuestAvailable(string definitionId);

    // Total-days value of the last day this definition's trigger fired, or null if
    // it has never fired (or no save is loaded). Pair with Game1.Date.TotalDays to
    // compute "days since last fire" for tooltips or "next fire in N days" hints.
    int? GetLastFiredDay(string definitionId);

    // True when this OneShot quest has already burned its one firing for the save.
    // Returns null if the id is unknown or no save is loaded. Always false for
    // non-OneShot quests since they don't use this flag.
    bool? GetOneShotFired(string definitionId);

    // Snapshot of every quest currently posted on a custom board. Mirrors
    // RegisteredQuestIds in that it's safe to call from anywhere after RegistrationClosed.
    // Pass an owner+name pair to filter to a single board, or leave both null/empty
    // to enumerate every board. Slots refresh at day-start and when a board is forced
    // to re-roll, so callers should re-query rather than caching.
    IReadOnlyList<CustomBoardSlotInfo> GetCustomBoardSlots(string? boardOwnerUniqueId = null, string? boardName = null);

    // Read-only snapshot of every step on an AdventureQuest. Null when the quest
    // isn't an AdventureQuest. Each AdventureStepInfo carries name, kind, progress,
    // count, done/active flags, description, and the raw Requires/Targets/Items
    // lists so a journal UI can render multi-step quests without reflection.
    IReadOnlyList<AdventureStepInfo>? GetAdventureSteps(Quest quest);

    // Index of the first not-Done step whose Requires[] are all Done, or null when
    // the quest is vanilla / not an AdventureQuest, or every step is done.
    int? GetActiveStepIndex(Quest quest);

    // Best-effort NPC name for the quest's giver. Reads AdventureQuest.giverNpc
    // for framework Adventure quests; falls back to vanilla subclass target fields
    // (ItemDeliveryQuest.target, SlayMonsterQuest.target). Returns null when no
    // giver can be inferred.
    string? GetGiverNpc(Quest quest);

    // Itemised reward lines for a quest carrying an IRewardedQuest payload (i.e.
    // any framework quest). Empty for vanilla quests. Each line is pre-translated
    // and carries enough side data (ItemId, NpcName, Amount, DurationDays, ...)
    // for a UI to render an icon or chip beside the text.
    IReadOnlyList<QuestRewardLine> GetRewardLines(Quest quest);

    // Cheat / debug helper: advances a Custom-kind AdventureStep by `amount`.
    // Returns false (no-op) for vanilla quests, non-Adventure quests, out-of-range
    // indices, non-Custom step kinds, completed quests, or steps whose Requires
    // aren't met yet. For amount < 0 the step is force-completed (Progress jumps
    // to Count). For amount >= remaining, the step is marked done.
    bool TryAdvanceCustomStep(Quest quest, int stepIndex, int amount);

    // Re-rolls today's daily-board batch. Mail-triggered postings are NOT re-rolled
    // (would risk double-posting mail flags).
    void RefreshOffers();

    // Returns null if no met NPC is in the pool or the role has no live entries.
    string? PickDispatchNpc(string role);

    IReadOnlyList<string> GetDispatchPool(string role);

    IReadOnlyList<string> GetMetHumanNpcs();

    // Pool is auto-populated each SaveLoaded by scanning Data/Objects for edibles
    // with non-zero Attack/Defense buffs. Use this for items that scan misses.
    void RegisterCombatFood(string itemId, int? magnitude = null);

    IReadOnlyList<string> GetCombatFoodPool();

    // Magnitude = floor(max(Attack, Defense)) across the item's buffs. Null when the
    // id isn't in the pool or was added without a magnitude.
    int? GetCombatFoodMagnitude(string qualifiedItemId);

    // Fires on the framework's first UpdateTicking. Subscribe here to register your
    // own quests, generators, custom quest types, dispatch NPCs, etc.
    event EventHandler RegistrationOpen;
    // Fires the same tick, right after every Open handler has run and the framework has
    // loaded its owned content packs. The registry is frozen at this point, so this is
    // the event to use if you want to inspect what other mods registered (quest browsers,
    // debug menus, GMCM helpers). Further RegisterQuest calls log and no-op.
    event EventHandler RegistrationClosed;
    event EventHandler<QuestAcceptedArgs> QuestAccepted;
    event EventHandler<QuestCompletedArgs> QuestCompleted;
    event EventHandler<QuestRemovedArgs> QuestRemoved;
    event EventHandler<DayRefreshedArgs> DayRefreshed;
    // Fires when a registered quest definition was considered for a daily fire but
    // skipped, either because its JSON conditions came back false or because the
    // trigger gate (cooldown, predicate, building/mail diff) didn't fire today.
    // Useful for "why didn't quest X show up?" debug menus. Daily-board pool picks
    // already log skip reasons at Debug, so this event covers the event-driven and
    // SpecialOrder paths that don't have a log line.
    event EventHandler<QuestSkippedArgs> QuestSkippedToday;
    // Fires when the player returns to title (leaves the loaded save). The framework's
    // save-bound state is torn down right after, so consumer mods should drop any cached
    // references to QuestInfo / IsManagedQuest / GetActiveCustomSteps results in this
    // handler. Also fires from Dispose when SMAPI tears the mod down. Re-registration
    // is NOT required between saves: quest definitions, generators, custom triggers,
    // etc. survive across save loads in the same session.
    event EventHandler FrameworkShuttingDown;

    // Fires on every successful Crop.harvest call (player + Junimo). Subscribe for
    // quests that need to track harvest events. Cheap to subscribe but the handler
    // runs on the game thread inside the harvest call, so keep work light.
    event EventHandler<CropHarvestInfo> CropHarvested;
}
