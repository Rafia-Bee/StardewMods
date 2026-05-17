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

    // Re-rolls today's daily-board batch. Mail-triggered postings are NOT re-rolled
    // (would risk double-posting mail flags).
    void RefreshOffers();

    void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null);

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
}
