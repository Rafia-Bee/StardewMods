using System.Collections.Generic;
using StardewValley;

namespace MoreQuestsFramework;

/// Tracks the absolute day index at which each definition was last posted, so the pipeline can
/// honour per-definition cooldowns. Also keeps short queues of recent items + NPCs for tie-breaking.
public sealed class AntiRepetition
{
    private const int MaxItemHistory = 6;
    private const int MaxNpcHistory = 3;

    private readonly Queue<string> _recentItems = new();
    private readonly Queue<string> _recentNpcs = new();
    private Dictionary<string, int> _lastPostedDay = new();
    private Dictionary<string, int> _dayStartSnapshot = new();

    public bool ItemRecent(string id) => _recentItems.Contains(id);
    public bool NpcRecent(string name) => _recentNpcs.Contains(name);

    /// Snapshots `_lastPostedDay` so a same-day re-roll (mq_refresh) can rewind today's
    /// records and avoid blocking every just-posted definition on its own cooldown.
    public void BeginDay()
    {
        _dayStartSnapshot = new Dictionary<string, int>(_lastPostedDay);
    }

    /// Restores `_lastPostedDay` to the day-start snapshot so today's postings can be
    /// re-rolled without their freshly-recorded cooldown entries blocking the new pool.
    public void RewindToDayStart()
    {
        _lastPostedDay = new Dictionary<string, int>(_dayStartSnapshot);
    }

    /// True if the definition's cooldown hasn't elapsed yet.
    public bool DefinitionOnCooldown(string id, int cooldownDays)
    {
        if (cooldownDays <= 0)
            return false;
        if (!_lastPostedDay.TryGetValue(id, out int lastDay))
            return false;
        return Game1.Date.TotalDays - lastDay < cooldownDays;
    }

    public void Record(QuestPosting posting)
    {
        if (!string.IsNullOrEmpty(posting.ObjectiveItemId))
            Push(_recentItems, posting.ObjectiveItemId, MaxItemHistory);
        if (!string.IsNullOrEmpty(posting.QuestGiver))
            Push(_recentNpcs, posting.QuestGiver, MaxNpcHistory);
        if (!string.IsNullOrEmpty(posting.DefinitionId))
            _lastPostedDay[posting.DefinitionId] = Game1.Date.TotalDays;
    }

    private static void Push(Queue<string> q, string v, int max)
    {
        q.Enqueue(v);
        while (q.Count > max)
            q.Dequeue();
    }
}
