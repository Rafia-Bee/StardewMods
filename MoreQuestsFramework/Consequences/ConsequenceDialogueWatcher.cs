using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

// Pop rule: at most one line per NPC per chat session AND per in-game day. Without
// the per-day clamp, a player who skips days gets every queued line back-to-back.
internal sealed class ConsequenceDialogueWatcher
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

        // Cutscenes/festivals: otherwise overwrites scripted dialogue (Krobus vault, etc.).
        if (Game1.eventUp || Game1.CurrentEvent != null)
            return;

        var speaker = Game1.currentSpeaker;
        if (speaker == null)
        {
            _lastSpeaker = null;
            return;
        }
        if (_lastSpeaker == speaker)
            return;
        _lastSpeaker = speaker;

        int today = Game1.Date?.TotalDays ?? 0;
        string speakerName = speaker.Name;

        if (_state.LastConsequencePoppedDay.TryGetValue(speakerName, out int lastDay) && lastDay >= today)
            return;

        // Pick the most-recent eligible entry (greatest EarliestFireDay <= today). If
        // the player skipped chat days, older queued chain lines would be out-of-order.
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
            // Portrait token MUST be at the end: SDV's parser switches when it hits
            // the token, so a leading-position token only renders for one frame.
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
