using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

/// Public framework-wide handle. Fetched once per consumer mod via
/// `helper.ModRegistry.GetApi&lt;IMoreQuestsApi&gt;("RafiaBee.MoreQuestsFramework")`,
/// then narrowed to a per-mod scope via `GetModApi(ModManifest)` for any registration call.
///
/// Lifecycle:
///   1. SMAPI fires `GameLaunched` for every mod in dependency order.
///   2. The framework fires `RegistrationOpen` from its own `GameLaunched` handler;
///      consumer mods do all their `RegisterQuest` / `RegisterGenerator` /
///      `LoadContentPack` / `RegisterDispatchNpc` work here.
///   3. After every owned content pack auto-loads (one tick later), the framework
///      fires `RegistrationClosed` and freezes the registry for the rest of the session.
///
/// API stability: marked Beta in Phase 5; freezes at Phase 10's framework v1.0.
public interface IMoreQuestsApi
{
    /// Returns a per-mod scope. Tracks ownership so registrations are namespaced and
    /// the framework can attribute `QuestAccepted` / `QuestCompleted` events back to
    /// the mod that registered the quest.
    IMoreQuestsModApi GetModApi(IManifest mod);

    /// True if the framework posted this Quest (via any registered consumer mod).
    /// Use this to filter SMAPI quest-log events down to framework-managed quests.
    bool IsManagedQuest(Quest quest);

    /// Re-rolls today's daily-board batch through the same path `OnDayStarted` uses.
    /// Test/iteration aid; safe to call after save load. Mail-triggered postings are
    /// not re-rolled (would risk double-posting mail flags).
    void RefreshOffers();

    /// Convenience wrapper around `GetModApi(...).RegisterDispatchNpc(...)` for callers
    /// that want a one-liner. The framework's own seed registrations use this path so
    /// the built-ins have no privileged route.
    void RegisterDispatchNpc(string role, string npcName, string? requiredModUniqueId = null);

    /// Picks the best NPC for a dispatch role (e.g. `DispatchRoles.CombatNpcs`).
    /// Restricted to NPCs the player has met; returns null if no met NPC is in the
    /// pool or the role has no live entries.
    string? PickDispatchNpc(string role);

    /// All NPCs currently eligible for the role (after met/exists filtering).
    /// Useful for content that wants to enumerate the pool itself.
    IReadOnlyList<string> GetDispatchPool(string role);

    /// All villager NPCs the player has met. Drop-in replacement for the legacy
    /// `NpcDispatch.MetHumanNpcs()` static helper.
    IReadOnlyList<string> GetMetHumanNpcs();

    /// Adds a qualified item id to the shared combat-buff food pool. The framework
    /// auto-populates the pool on every `SaveLoaded` by scanning `Data/Objects` for
    /// edibles with non-zero Attack or Defense buffs, so most consumer mods don't
    /// need to call this. Use it to add items the scan misses (modded foods that
    /// grant attack via a non-standard mechanism, custom rings, etc.). Optional
    /// `magnitude` records the food's buff strength so reward generators can bucket
    /// rewards by tier; pass null if you just want the id in the pool without a
    /// magnitude lookup. Duplicate ids are ignored.
    void RegisterCombatFood(string itemId, int? magnitude = null);

    /// Snapshot of every item id currently in the combat-food pool. Returns the
    /// live list as a read-only view; callers should treat it as a snapshot.
    IReadOnlyList<string> GetCombatFoodPool();

    /// Magnitude (max of Attack / Defense buff levels, floored) recorded for a
    /// combat-food item id. Returns null if the id isn't in the pool or was added
    /// without a magnitude. Used by reward generators to filter rewards by tier
    /// (e.g. +1 / +2 / +3 buckets for the Monster Hunt quest).
    int? GetCombatFoodMagnitude(string qualifiedItemId);

    /// Fired during the framework's `GameLaunched` handler, before content-pack
    /// auto-loading runs. Subscribe to register quests / generators / dispatch entries.
    event EventHandler RegistrationOpen;
    /// Fired one tick after `GameLaunched`, after every owned content pack has
    /// auto-loaded. The registry is frozen at this point; further `RegisterQuest`
    /// calls log a warning and no-op.
    event EventHandler RegistrationClosed;
    event EventHandler<QuestAcceptedArgs> QuestAccepted;
    event EventHandler<QuestCompletedArgs> QuestCompleted;
    event EventHandler<QuestRemovedArgs> QuestRemoved;
    event EventHandler<DayRefreshedArgs> DayRefreshed;
}
