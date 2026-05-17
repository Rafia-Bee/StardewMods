using System;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

// Shared pop logic for the two dialogue paths: the checkAction prefix (player walks
// up and presses use) and the 1Hz watcher backup (NPC-initiated greetings, scripted
// drawDialogue calls). Both need the same per-day clamp and stale-entry sweep, so
// the algorithm lives here and each caller wraps it with its own delivery rules.
internal static class ConsequenceDialogueDispatcher
{
    public static bool TryDequeueBestEntry(
        FrameworkState state,
        string npcName,
        int today,
        out DialogueQueueEntry? chosen,
        out int dropped)
    {
        chosen = null;
        dropped = 0;

        var queue = state.PendingConsequenceLines;
        if (queue.Count == 0)
            return false;

        // Cutscenes/festivals: otherwise overwrites scripted dialogue (Krobus vault, etc.).
        if (Game1.eventUp || Game1.CurrentEvent != null)
            return false;

        // Per-day clamp so a player who skips days doesn't get every queued chain line
        // back-to-back on the next chat.
        if (state.LastConsequencePoppedDay.TryGetValue(npcName, out int lastDay) && lastDay >= today)
            return false;

        // Pick the most-recent eligible entry (greatest EarliestFireDay <= today), so
        // chained beats stay in narrative order even when the player skipped a chat day.
        int bestIdx = -1;
        int bestDay = int.MinValue;
        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, npcName, StringComparison.OrdinalIgnoreCase))
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
            return false;

        chosen = queue[bestIdx];
        // Back-to-front so RemoveAt indices stay valid. Drop stale earlier-day entries.
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (i == bestIdx) continue;
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, npcName, StringComparison.OrdinalIgnoreCase))
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
        state.LastConsequencePoppedDay[npcName] = today;
        return true;
    }

    // drawNow is true for the 1Hz watcher (NPC-initiated speech where vanilla won't
    // redraw on its own) and false for the checkAction prefix (vanilla draws after).
    // Portrait token MUST be at the end; leading placement only renders for one frame.
    public static void ApplyEntry(
        NPC speaker,
        Farmer who,
        DialogueQueueEntry entry,
        bool drawNow,
        IMonitor monitor,
        string source)
    {
        if (entry.FriendshipDelta != 0 && who != null)
        {
            int before = who.getFriendshipLevelForNPC(speaker.Name);
            who.changeFriendship(entry.FriendshipDelta, speaker);
            int after = who.getFriendshipLevelForNPC(speaker.Name);
            monitor.Log(
                $"{source}: {speaker.Name} friendship {before} -> {after} (delta {entry.FriendshipDelta:+#;-#;0}). If unchanged, check that 'no friendship decay' / friendship-clamp mods aren't intercepting changeFriendship.",
                LogLevel.Debug);
        }

        if (!string.IsNullOrEmpty(entry.Line))
        {
            string text = string.IsNullOrEmpty(entry.Portrait)
                ? entry.Line
                : entry.Line + entry.Portrait;
            speaker.CurrentDialogue.Push(new Dialogue(speaker, null, text));
            if (drawNow)
                Game1.drawDialogue(speaker);
        }
    }
}
