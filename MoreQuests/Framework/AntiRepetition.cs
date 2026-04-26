using System.Collections.Generic;
using System.Linq;

namespace MoreQuests.Framework;

internal sealed class AntiRepetition
{
    private const int MaxItemHistory = 6;
    private const int MaxNpcHistory = 3;
    private const int MaxDefinitionHistory = 4;

    private readonly Queue<string> _recentItems = new();
    private readonly Queue<string> _recentNpcs = new();
    private readonly Queue<string> _recentDefinitions = new();

    public bool ItemRecent(string id) => _recentItems.Contains(id);
    public bool NpcRecent(string name) => _recentNpcs.Contains(name);
    public bool DefinitionRecent(string id) => _recentDefinitions.Contains(id);

    public void Record(QuestPosting posting)
    {
        if (!string.IsNullOrEmpty(posting.ObjectiveItemId))
            Push(_recentItems, posting.ObjectiveItemId, MaxItemHistory);
        if (!string.IsNullOrEmpty(posting.QuestGiver))
            Push(_recentNpcs, posting.QuestGiver, MaxNpcHistory);
        if (!string.IsNullOrEmpty(posting.DefinitionId))
            Push(_recentDefinitions, posting.DefinitionId, MaxDefinitionHistory);
    }

    private static void Push(Queue<string> q, string v, int max)
    {
        q.Enqueue(v);
        while (q.Count > max)
            q.Dequeue();
    }
}
