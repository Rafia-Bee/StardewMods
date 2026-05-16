using System.Collections.Generic;
using StardewValley;

namespace MoreQuestsFramework;

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

    // Snapshot so mq_refresh same-day re-rolls don't block every just-posted
    // definition on its own freshly-recorded cooldown.
    public void BeginDay()
    {
        _dayStartSnapshot = new Dictionary<string, int>(_lastPostedDay);
    }

    public void RewindToDayStart()
    {
        _lastPostedDay = new Dictionary<string, int>(_dayStartSnapshot);
    }

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
