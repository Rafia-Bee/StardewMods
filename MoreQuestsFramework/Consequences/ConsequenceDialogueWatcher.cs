using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

/// Pops queued consequence dialogue lines the next time the player chats with the
/// affected NPC. Mirrors `Triggers/DialogueWatcher` (the NpcDialogue trigger source) —
/// 1 Hz tick on `OneSecondUpdateTicked`, no Harmony patch, stays inside §8.7's patch
/// budget.
///
/// Pop rule: at most one line per NPC per chat session. `EarliestFireDay` is honoured
/// so Tier 3 chains spread across consecutive days; entries scheduled for tomorrow
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

        // Pop the first matching entry whose EarliestFireDay has arrived. We pop one
        // entry per chat to avoid blasting the player with three days' worth of Tier 3
        // lines on a single conversation.
        for (int i = 0; i < _state.PendingConsequenceLines.Count; i++)
        {
            var entry = _state.PendingConsequenceLines[i];
            if (!string.Equals(entry.NpcName, speakerName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.EarliestFireDay > today)
                continue;

            _state.PendingConsequenceLines.RemoveAt(i);
            FireOne(speaker, entry);
            return;
        }
    }

    private void FireOne(NPC speaker, DialogueQueueEntry entry)
    {
        if (entry.FriendshipDelta != 0 && Game1.player != null)
            Game1.player.changeFriendship(entry.FriendshipDelta, speaker);

        if (!string.IsNullOrEmpty(entry.Line))
        {
            string text = string.IsNullOrEmpty(entry.Portrait)
                ? entry.Line
                : entry.Portrait + " " + entry.Line;
            speaker.CurrentDialogue.Push(new Dialogue(speaker, null, text));
            Game1.drawDialogue(speaker);
        }

        _monitor.Log(
            $"ConsequenceDialogueWatcher: popped line for {speaker.Name} (friendship {entry.FriendshipDelta:+#;-#;0}, portrait '{entry.Portrait}').",
            LogLevel.Trace);
    }
}
