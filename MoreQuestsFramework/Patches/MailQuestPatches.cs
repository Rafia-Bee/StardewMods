using HarmonyLib;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Patches;

// Vanilla's getQuestFromId lookup is a hard-coded switch on quest-type strings, so
// without this prefix our framework Quest subclasses would be replaced by vanilla
// classes and the declarative reward payout would silently break.
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

    public static bool GetQuestFromId_Prefix(string id, ref Quest __result)
    {
        if (string.IsNullOrEmpty(id))
            return true;
        if (!_registry.TryGet(id, out var entry) || entry.Quest == null)
            return true;

        var quest = entry.Quest;
        // showNew matches vanilla's getQuestFromId so the "!" indicator and quest-tracking
        // mods key off it normally.
        quest.id.Value = id;
        quest.showNew.Value = true;
        __result = quest;

        // Re-read of a saved letter still calls getQuestFromId. Keep the entry around so
        // the second read returns the same instance, but only TrackPosted on first hand-off
        // (the quest is already in the log after that, and TrackPosted would double-fire).
        if (!_registry.IsHandedOff(id))
        {
            _api.TrackPosted(quest, entry.OwnerUniqueId, entry.DefinitionId);
            _registry.MarkHandedOff(id);
            _monitor.Log($"Mail-quest hand-off: returned framework Quest for id '{id}'.", LogLevel.Trace);
        }
        return false;
    }
}
