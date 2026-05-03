using HarmonyLib;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

/// Harmony prefix on `NPC.checkAction` that pops a pending consequence line for the
/// target NPC (if any) before vanilla builds + draws the dialogue. Without this hook,
/// the 1 Hz `ConsequenceDialogueWatcher.Tick` fires *after* vanilla has already drawn
/// the normal dialogue, producing a visible flash where the normal line shows for a
/// frame and then gets replaced by the consequence line on the next tick.
///
/// Patch is fully gated per §8.1 — when no consequence lines are queued, the prefix
/// returns immediately after one int compare. The watcher stays as a backup for any
/// dialogue path that doesn't go through `checkAction` (e.g. NPC-initiated greetings
/// or scripted events that call `Game1.drawDialogue` directly), so a queued line is
/// never "stuck" if the patch misses an edge case.
internal static class ConsequenceDialoguePatches
{
    /// Live framework state set by `ModEntry.OnSaveLoaded` to point at the per-save
    /// `FrameworkState` instance. Null between saves; the prefix no-ops on null so
    /// patch overhead is one ref-equals + one int compare when no save is loaded.
    public static FrameworkState? ActiveState { get; set; }
    private static IMonitor? _monitor;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(ConsequenceDialoguePatches), nameof(CheckAction_Prefix)));
    }

    /// `__instance` is the NPC the player just clicked. Returns void so vanilla
    /// continues to its own `checkAction` body — we just slip a dialogue onto the top
    /// of `currentDialogue` first, which vanilla will then pop + draw normally.
    /// `who` is the farmer; `l` is the location. Neither is needed here but the
    /// prefix signature has to match Harmony's matching rules.
    public static void CheckAction_Prefix(NPC __instance, Farmer who)
    {
        if (ActiveState == null || __instance == null) return;
        var queue = ActiveState.PendingConsequenceLines;
        if (queue.Count == 0) return;

        int today = Game1.Date?.TotalDays ?? 0;
        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, __instance.Name, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.EarliestFireDay > today)
                continue;

            queue.RemoveAt(i);
            FireOne(__instance, who, entry);
            return;
        }
    }

    private static void FireOne(NPC speaker, Farmer who, DialogueQueueEntry entry)
    {
        if (entry.FriendshipDelta != 0 && who != null)
            who.changeFriendship(entry.FriendshipDelta, speaker);

        if (!string.IsNullOrEmpty(entry.Line))
        {
            string text = string.IsNullOrEmpty(entry.Portrait)
                ? entry.Line
                : entry.Portrait + " " + entry.Line;
            speaker.CurrentDialogue.Push(new Dialogue(speaker, null, text));
        }

        _monitor?.Log(
            $"ConsequenceDialoguePatches: pushed line for {speaker.Name} via checkAction (friendship {entry.FriendshipDelta:+#;-#;0}, portrait '{entry.Portrait}').",
            LogLevel.Trace);
    }
}
