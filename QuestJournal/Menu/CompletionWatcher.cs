using System.Collections.Generic;
using QuestJournal.Api;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;

namespace QuestJournal.Menu;

// Watches the player's quest log every tick and notices when a quest gets completed or
// drops off (failed). When that happens it snapshots the quest details and rewards and
// saves a record to the completed-quest store so the journal's history tab can show it.
public sealed class CompletionWatcher
{
    private readonly IModHelper _helper;
    private readonly IMoreQuestsApi? _mqfApi;
    private readonly Dictionary<Quest, QuestSnapshot> _tracked = new();
    private readonly HashSet<Quest> _recorded = new();
    private readonly HashSet<Quest> _ignored = new();

    public CompletionWatcher(IModHelper helper, IMoreQuestsApi? mqfApi)
    {
        _helper = helper;
        _mqfApi = mqfApi;
    }

    public void Register()
    {
        _helper.Events.GameLoop.SaveLoaded += OnReset;
        _helper.Events.GameLoop.ReturnedToTitle += OnReset;
        _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    public void MarkRecorded(Quest quest)
    {
        if (quest != null) _recorded.Add(quest);
    }

    public void MarkIgnore(Quest quest)
    {
        if (quest != null) _ignored.Add(quest);
    }

    private void OnReset(object? sender, System.EventArgs e)
    {
        _tracked.Clear();
        _recorded.Clear();
        _ignored.Clear();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady) return;
        var farmer = Game1.player;
        if (farmer == null) return;

        var current = new HashSet<Quest>();
        foreach (var q in farmer.questLog)
        {
            if (q == null) continue;
            current.Add(q);

            if (!_tracked.TryGetValue(q, out var snap))
            {
                snap = Capture(q);
                _tracked[q] = snap;
            }
            else
            {
                snap.Objective = q.currentObjective ?? snap.Objective;
            }

            if (q.completed.Value && !_recorded.Contains(q))
            {
                snap.LastSeenCompleted = true;

                if (q.HasReward()) continue;

                Record(snap, "Completed");
                _recorded.Add(q);
            }
        }

        List<Quest>? toRemove = null;
        foreach (var kv in _tracked)
        {
            if (current.Contains(kv.Key)) continue;
            (toRemove ??= new List<Quest>()).Add(kv.Key);
            if (_recorded.Contains(kv.Key)) continue;
            if (_ignored.Contains(kv.Key)) continue;
            bool wasCompleted = kv.Value.LastSeenCompleted || kv.Key.completed.Value;
            string status = wasCompleted ? "Completed" : "Failed";
            Record(kv.Value, status);
            _recorded.Add(kv.Key);
        }
        if (toRemove != null)
            foreach (var k in toRemove) _tracked.Remove(k);
    }

    private QuestSnapshot Capture(Quest q)
    {
        var rewards = QuestSnapshotBuilder.BuildRewardLines(q, _mqfApi);
        var (category, kind) = QuestSnapshotBuilder.ResolveCategoryKind(q, _mqfApi);
        return new QuestSnapshot
        {
            Title = q.questTitle ?? string.Empty,
            Description = q.questDescription ?? string.Empty,
            Objective = q.currentObjective ?? string.Empty,
            Rewards = rewards,
            Giver = QuestSnapshotBuilder.ResolveGiverDisplay(q, _mqfApi),
            Source = QuestSnapshotBuilder.ResolveSourceDisplay(q, _mqfApi, _helper),
            Category = category,
            Kind = kind,
            LastSeenCompleted = q.completed.Value
        };
    }

    private static void Record(QuestSnapshot snap, string status)
    {
        var stored = new List<StoredRewardLine>();
        var aggregate = new List<string>();
        foreach (var r in snap.Rewards)
        {
            stored.Add(new StoredRewardLine
            {
                Kind = r.Kind,
                Summary = r.Summary,
                ItemId = r.ItemId,
                NpcName = r.NpcName,
                Amount = r.Amount,
                DurationDays = r.DurationDays
            });
            if (!string.IsNullOrEmpty(r.Summary)) aggregate.Add(r.Summary);
        }

        CompletedQuestStore.Add(new CompletedQuestRecord
        {
            Title = snap.Title,
            Description = snap.Description,
            Objective = snap.Objective,
            RewardSummary = string.Join(", ", aggregate),
            RewardLines = stored,
            Giver = snap.Giver,
            Source = snap.Source,
            Category = snap.Category,
            Kind = snap.Kind,
            CompletedOnTotalDays = Game1.Date?.TotalDays ?? 0,
            Status = status
        });
    }

    private sealed class QuestSnapshot
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public List<RewardLineRow> Rewards { get; set; } = new();
        public string Giver { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool LastSeenCompleted { get; set; }
    }
}
