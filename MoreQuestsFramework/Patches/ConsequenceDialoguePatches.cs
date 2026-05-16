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

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(ConsequenceDialoguePatches), nameof(CheckAction_Prefix)));
    }

    public static void CheckAction_Prefix(NPC __instance, Farmer who)
    {
        if (ActiveState == null || __instance == null) return;
        var queue = ActiveState.PendingConsequenceLines;
        if (queue.Count == 0) return;

        // Cutscenes/festivals would otherwise overwrite scripted dialogue.
        if (Game1.eventUp || Game1.CurrentEvent != null) return;

        int today = Game1.Date?.TotalDays ?? 0;
        string npcName = __instance.Name;

        // Per-day clamp so a player who skips days doesn't get every queued chain line
        // back-to-back on the next chat.
        if (ActiveState.LastConsequencePoppedDay.TryGetValue(npcName, out int lastDay) && lastDay >= today)
            return;

        // Pick the most-recent eligible entry (greatest EarliestFireDay <= today), so
        // chained beats stay in narrative order even when the player skipped a chat day.
        int bestIdx = -1;
        int bestDay = int.MinValue;
        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, npcName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.EarliestFireDay > today)
                continue;
            if (entry.EarliestFireDay > bestDay)
            {
                bestDay = entry.EarliestFireDay;
                bestIdx = i;
            }
        }
        if (bestIdx < 0)
            return;

        var chosen = queue[bestIdx];
        // Back-to-front so RemoveAt indices stay valid. Drop stale earlier-day entries.
        int dropped = 0;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (i == bestIdx) continue;
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, npcName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.EarliestFireDay > today)
                continue;
            if (entry.EarliestFireDay >= bestDay)
                continue;
            queue.RemoveAt(i);
            dropped++;
            if (i < bestIdx) bestIdx--;
        }

        queue.RemoveAt(bestIdx);
        ActiveState.LastConsequencePoppedDay[npcName] = today;
        if (dropped > 0)
            _monitor?.Log(
                $"ConsequenceDialoguePatches: dropped {dropped} stale chain entries for {npcName} (player skipped earlier chat days).",
                LogLevel.Trace);
        FireOne(__instance, who, chosen);
    }

    private static void FireOne(NPC speaker, Farmer who, DialogueQueueEntry entry)
    {
        if (entry.FriendshipDelta != 0 && who != null)
        {
            int before = who.getFriendshipLevelForNPC(speaker.Name);
            who.changeFriendship(entry.FriendshipDelta, speaker);
            int after = who.getFriendshipLevelForNPC(speaker.Name);
            _monitor?.Log(
                $"ConsequenceDialoguePatches: {speaker.Name} friendship {before} -> {after} (delta {entry.FriendshipDelta:+#;-#;0}). If unchanged, check that 'no friendship decay' / friendship-clamp mods aren't intercepting changeFriendship.",
                LogLevel.Debug);
        }

        if (!string.IsNullOrEmpty(entry.Line))
        {
            // Portrait token MUST be at the end; leading placement only renders for one frame.
            string text = string.IsNullOrEmpty(entry.Portrait)
                ? entry.Line
                : entry.Line + entry.Portrait;
            speaker.CurrentDialogue.Push(new Dialogue(speaker, null, text));
        }

        _monitor?.Log(
            $"ConsequenceDialoguePatches: pushed line for {speaker.Name} via checkAction (friendship {entry.FriendshipDelta:+#;-#;0}, portrait '{entry.Portrait}').",
            LogLevel.Trace);
    }
}
