using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

// Pop rule: at most one line per NPC per chat session AND per in-game day. Without
// the per-day clamp, a player who skips days gets every queued line back-to-back.
internal sealed class ConsequenceDialogueWatcher
{
    private const string Source = "ConsequenceDialogueWatcher";

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
            return;
        _lastSpeaker = speaker;

        int today = Game1.Date?.TotalDays ?? 0;
        if (!ConsequenceDialogueDispatcher.TryDequeueBestEntry(_state, speaker.Name, today, out var chosen, out int dropped))
            return;

        if (dropped > 0)
            ModEntry.LogDebug($"{Source}: dropped {dropped} stale chain entries for {speaker.Name} (player skipped earlier chat days).");

        ConsequenceDialogueDispatcher.ApplyEntry(speaker, Game1.player, chosen!, drawNow: true, _monitor, Source);
    }
}
