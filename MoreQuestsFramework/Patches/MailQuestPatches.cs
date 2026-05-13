using HarmonyLib;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Patches;

/// Harmony prefix on `Quest.getQuestFromId(string)` so the standard
/// `%item quest <id> 1 %%` mail token can return our framework Quest subclasses.
/// Vanilla's lookup is a hard-coded switch on quest type strings, without this
/// patch our `MoreQuestsItemDeliveryQuest` (and its `serializedRewards` NetField)
/// would be replaced by a vanilla `ItemDeliveryQuest` and our declarative reward
/// payout would silently break.
///
/// Patch is **gated**: when the registry has no pending entries (the common case),
/// the prefix is one dictionary lookup that returns true and lets vanilla run.
/// Aligns with §8.1, "every patch returns immediately if no active quest needs it".
internal static class MailQuestPatches
{
    private static MailQuestRegistry _registry = null!;
    private static MoreQuestsApi _api = null!;
    private static IMonitor _monitor = null!;

    public static void Apply(Harmony harmony, MailQuestRegistry registry, MoreQuestsApi api, IMonitor monitor)
    {
        _registry = registry;
        _api = api;
        _monitor = monitor;

        harmony.Patch(
            original: AccessTools.Method(typeof(Quest), nameof(Quest.getQuestFromId), new[] { typeof(string) }),
            prefix: new HarmonyMethod(typeof(MailQuestPatches), nameof(GetQuestFromId_Prefix)));
    }

    /// True (continue to vanilla) when no entry matches; false (skip vanilla)
    /// when we have a prepared Quest for this id. Sets `showNew = true` on the
    /// returned Quest to match what `Quest.getQuestFromId` does on its own
    /// vanilla path, that's the field the vanilla "!" indicator and most
    /// quest-tracking mods key off. Also tracks the Quest with the public API
    /// so `QuestAccepted` / `QuestCompleted` / `QuestRemoved` fire correctly.
    public static bool GetQuestFromId_Prefix(string id, ref Quest __result)
    {
        if (string.IsNullOrEmpty(id))
            return true;
        if (!_registry.TryGet(id, out var entry) || entry.Quest == null)
            return true;

        var quest = entry.Quest;
        quest.id.Value = id;
        quest.showNew.Value = true;
        _api.TrackPosted(quest, entry.OwnerUniqueId, entry.DefinitionId);
        __result = quest;
        // Removed from the registry once handed off, `Farmer.addQuest` adds
        // the returned Quest to `questLog` immediately after, so the framework
        // no longer needs the prepared instance.
        _registry.Remove(id);
        _monitor.Log($"Mail-quest hand-off: returned framework Quest for id '{id}'.", LogLevel.Trace);
        return false;
    }
}
