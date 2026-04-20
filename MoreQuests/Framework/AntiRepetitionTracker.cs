using System.Collections.Generic;
using System.Linq;

namespace MoreQuests.Framework;

/// <summary>
/// Prevents quest objectives from repeating too frequently.
/// Tracks recently used items and NPCs per category.
/// </summary>
internal sealed class AntiRepetitionTracker
{
    private const int MaxHistory = 5;
    private const int MaxNpcHistory = 3;

    private readonly Dictionary<QuestCategory, Queue<string>> _recentItems = new();
    private readonly Queue<string> _recentNpcs = new();

    public bool WasRecentlyUsed(string itemId)
    {
        return _recentItems.Values.Any(q => q.Contains(itemId));
    }

    public bool WasNpcRecentlyUsed(string npcName)
    {
        return _recentNpcs.Contains(npcName);
    }

    public void RecordQuest(GeneratedQuest quest)
    {
        // Track item
        if (!string.IsNullOrEmpty(quest.ObjectiveItemId))
        {
            if (!_recentItems.ContainsKey(quest.Category))
                _recentItems[quest.Category] = new Queue<string>();

            var queue = _recentItems[quest.Category];
            queue.Enqueue(quest.ObjectiveItemId);
            while (queue.Count > MaxHistory)
                queue.Dequeue();
        }

        // Track NPC
        if (!string.IsNullOrEmpty(quest.QuestGiverNpc))
        {
            _recentNpcs.Enqueue(quest.QuestGiverNpc);
            while (_recentNpcs.Count > MaxNpcHistory)
                _recentNpcs.Dequeue();
        }
    }
}
