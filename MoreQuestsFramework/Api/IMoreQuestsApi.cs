using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Lifecycle:
//   1. SMAPI fires GameLaunched for every mod in dependency order.
//   2. Framework fires RegistrationOpen during its own GameLaunched.
//   3. One tick later, after owned content packs auto-load, RegistrationClosed fires
//      and the registry freezes.
public interface IMoreQuestsApi
{
    IMoreQuestsModApi GetModApi(IManifest mod);

    bool IsManagedQuest(Quest quest);

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

    event EventHandler RegistrationOpen;
    // Registry is frozen after this fires; further RegisterQuest calls log and no-op.
    event EventHandler RegistrationClosed;
    event EventHandler<QuestAcceptedArgs> QuestAccepted;
    event EventHandler<QuestCompletedArgs> QuestCompleted;
    event EventHandler<QuestRemovedArgs> QuestRemoved;
    event EventHandler<DayRefreshedArgs> DayRefreshed;
}
