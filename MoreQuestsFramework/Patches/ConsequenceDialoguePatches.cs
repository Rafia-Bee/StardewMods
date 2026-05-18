using HarmonyLib;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Without this prefix, the 1Hz watcher fires AFTER vanilla drew the normal dialogue,
// producing a visible flash where the normal line shows for a frame then gets replaced.
// The watcher stays as a backup for dialogue paths that bypass checkAction (NPC-initiated
// greetings, scripted Game1.drawDialogue calls).
internal static class ConsequenceDialoguePatches
{
    public static FrameworkState? ActiveState { get; set; }
    private static IMonitor? _monitor;
    private const string Source = "ConsequenceDialoguePatches";

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(ConsequenceDialoguePatches), nameof(CheckAction_Prefix)));
    }

    public static void CheckAction_Prefix(NPC __instance, Farmer who)
    {
        if (ActiveState == null || __instance == null || _monitor == null) return;

        int today = Game1.Date?.TotalDays ?? 0;
        if (!ConsequenceDialogueDispatcher.TryDequeueBestEntry(ActiveState, __instance.Name, today, out var chosen, out int dropped))
            return;

        if (dropped > 0)
            ModEntry.LogDebug($"{Source}: dropped {dropped} stale chain entries for {__instance.Name} (player skipped earlier chat days).");

        ConsequenceDialogueDispatcher.ApplyEntry(__instance, who, chosen!, drawNow: false, _monitor, Source);
    }
}
