using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

/// Pops queued consequence dialogue lines the next time the player chats with the
/// affected NPC. Mirrors `Triggers/DialogueWatcher` (the NpcDialogue trigger source),
/// 1 Hz tick on `OneSecondUpdateTicked`, no Harmony patch, stays inside §8.7's patch
/// budget.
///
/// Pop rule: at most one line per NPC per chat session AND at most one line per NPC
/// per in-game day. Without the per-day clamp, a player who skips ahead with cheats
/// (or just doesn't talk to the NPC for a few days) gets every queued line back to
/// back on the next chat, breaks immersion. `EarliestFireDay` is honoured on top so
/// Tier 3 chains spread across consecutive days; entries scheduled for tomorrow
/// stay in the queue until the relevant `DayStarted`.
public sealed class ConsequenceDialogueWatcher
{
    private readonly FrameworkState _state;
    private readonly IMonitor _monitor;
    private NPC? _lastSpeaker;

    public ConsequenceDialogueWatcher(FrameworkState state, IMonitor monitor)
    {
        _state = state;
        _monitor = monitor;
    }

    public void Reset()
    {
        _lastSpeaker = null;
    }

    public void Tick()
    {
        if (!Context.IsWorldReady)
            return;
        if (_state.PendingConsequenceLines.Count == 0)
            return;

        // Skip during cutscenes / festival events. The intercept would otherwise
        // overwrite the scripted dialogue (Krobus's vault cutscene, etc.), players
        // get an out-of-context consequence line and lose the cutscene's intended one.
        if (Game1.eventUp || Game1.CurrentEvent != null)
            return;

        var speaker = Game1.currentSpeaker;
        if (speaker == null)
        {
            _lastSpeaker = null;
            return;
        }
        if (_lastSpeaker == speaker)
            return; // already handled this conversation
        _lastSpeaker = speaker;

        int today = Game1.Date?.TotalDays ?? 0;
        string speakerName = speaker.Name;

        // Per-day clamp, if a consequence already popped for this NPC today (via the
        // checkAction prefix or an earlier tick this conversation), don't pop another.
        if (_state.LastConsequencePoppedDay.TryGetValue(speakerName, out int lastDay) && lastDay >= today)
            return;

        // Pick the most-recent eligible entry for this NPC (greatest EarliestFireDay
        // that's still <= today). Tier 3 chains queue one entry per day; if the player
        // skipped a chat day, the queue holds an out-of-order older line that we don't
        // want surfacing now. See `ConsequenceDialoguePatches.CheckAction_Prefix` for
        // the matching logic.
        var queue = _state.PendingConsequenceLines;
        int bestIdx = -1;
        int bestDay = int.MinValue;
        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, speakerName, System.StringComparison.OrdinalIgnoreCase))
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
        int dropped = 0;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (i == bestIdx) continue;
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, speakerName, System.StringComparison.OrdinalIgnoreCase))
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
        _state.LastConsequencePoppedDay[speakerName] = today;
        if (dropped > 0)
            _monitor.Log(
                $"ConsequenceDialogueWatcher: dropped {dropped} stale chain entries for {speakerName} (player skipped earlier chat days).",
                LogLevel.Trace);
        FireOne(speaker, chosen);
    }

    private void FireOne(NPC speaker, DialogueQueueEntry entry)
    {
        if (entry.FriendshipDelta != 0 && Game1.player != null)
        {
            int before = Game1.player.getFriendshipLevelForNPC(speaker.Name);
            Game1.player.changeFriendship(entry.FriendshipDelta, speaker);
            int after = Game1.player.getFriendshipLevelForNPC(speaker.Name);
            _monitor.Log(
                $"ConsequenceDialogueWatcher: {speaker.Name} friendship {before} -> {after} (delta {entry.FriendshipDelta:+#;-#;0}). If unchanged, check that 'no friendship decay' / friendship-clamp mods aren't intercepting changeFriendship.",
                LogLevel.Debug);
        }

        if (!string.IsNullOrEmpty(entry.Line))
        {
            // Portrait code goes at the END of the dialogue per SDV's parser
            // convention, the portrait switches when the parser hits the token, so
            // a leading-position token only renders for one frame before the next
            // page wipes it. Wiki: "Portrait commands should be at the end of a
            // dialogue line".
            string text = string.IsNullOrEmpty(entry.Portrait)
                ? entry.Line
                : entry.Line + entry.Portrait;
            speaker.CurrentDialogue.Push(new Dialogue(speaker, null, text));
            Game1.drawDialogue(speaker);
        }

        _monitor.Log(
            $"ConsequenceDialogueWatcher: popped line for {speaker.Name} (friendship {entry.FriendshipDelta:+#;-#;0}, portrait '{entry.Portrait}').",
            LogLevel.Trace);
    }
}
