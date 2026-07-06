using System.Collections.Generic;
using MoreQuestsFramework.State;
using StardewValley;

namespace MoreQuestsFramework;

internal sealed class AntiRepetition
{
    private readonly Queue<string> _recentItems = new();
    private readonly Queue<string> _recentNpcs = new();
    private Dictionary<string, int> _lastPostedDay = new();
    private Dictionary<string, int> _dayStartSnapshot = new();

    public bool ItemRecent(string id) => _recentItems.Contains(id);
    public bool NpcRecent(string name) => _recentNpcs.Contains(name);

    // Switches the cooldown store from the in-memory placeholder to the persisted
    // dict on FrameworkState so daily/custom-board cooldowns survive reloads.
    public void WireState(FrameworkState state)
    {
        _lastPostedDay = state.AntiRepetitionLastPostedDay;
        _dayStartSnapshot = new Dictionary<string, int>(_lastPostedDay);
    }

    // Snapshot so mq_refresh same-day re-rolls don't block every just-posted
    // definition on its own freshly-recorded cooldown.
    public void BeginDay()
    {
        _dayStartSnapshot = new Dictionary<string, int>(_lastPostedDay);
    }

    public void RewindToDayStart()
    {
        _lastPostedDay.Clear();
        foreach (var kv in _dayStartSnapshot)
            _lastPostedDay[kv.Key] = kv.Value;
    }

    public bool DefinitionOnCooldown(string id, int cooldownDays)
    {
        if (cooldownDays <= 0)
            return false;
        if (!_lastPostedDay.TryGetValue(id, out int lastDay))
            return false;
        return Game1.Date.TotalDays - lastDay < cooldownDays;
    }

    // Tracks recent item / NPC choices so back-to-back postings don't repeat the same
    // target. Called at post time for every posting, board or not. Does NOT start the
    // definition-level cooldown; that's RecordDefinitionAccepted's job.
    public void RecordRecency(QuestPosting posting)
    {
        if (!string.IsNullOrEmpty(posting.ObjectiveItemId))
            RecencyWindow.Push(_recentItems, posting.ObjectiveItemId, System.Math.Max(0, ModEntry.Config.AntiRepetitionItemHistory));
        if (!string.IsNullOrEmpty(posting.QuestGiver))
            RecencyWindow.Push(_recentNpcs, posting.QuestGiver, System.Math.Max(0, ModEntry.Config.AntiRepetitionNpcHistory));
    }

    // Starts the definition-level cooldown clock. For board postings this fires when
    // the player actually accepts the slot, so an ignored quest stays re-rollable the
    // next day. For mail / one-shot / dialogue postings the caller invokes this at
    // post time since those channels don't have a separate accept step.
    public void RecordDefinitionAccepted(string? definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return;
        _lastPostedDay[definitionId] = Game1.Date.TotalDays;
    }
}
