using System.Collections.Generic;
using QuestJournal.Api;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;

namespace QuestJournal.Menu;

// Watches Game1.player.questLog every tick so quests completed by playing
// them (delivery, fishing, slay, etc.) make it into the Completed tab.
// CompletedQuestStore.Add was previously only called from the journal's
// Complete button, so natural completions were silently dropped.
//
// Approach: snapshot every active quest on first sight, refresh the
// objective each tick, and record the snapshot the moment we observe
// `quest.completed.Value` flip true. Removals without a completed flip
// (cancellations) are skipped so the Completed tab doesn't fill with
// cancelled rows.
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

    // JournalContext.CompleteSelected writes its own record before calling
    // quest.questComplete(), so we don't want the watcher to record a second
    // copy when it later sees completed.Value flip. Mark it as already-handled.
    public void MarkRecorded(Quest quest)
    {
        if (quest != null) _recorded.Add(quest);
    }

    // Tells the watcher "the player intentionally dropped this; don't
    // record it as Failed when it disappears". The journal's Cancel
    // button calls this right before removing the quest from the log.
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
                // Refresh the objective so the recorded snapshot reflects the
                // last live state. Rewards stay frozen at first sight: vanilla
                // questComplete zeros moneyReward, so re-reading post-flip
                // would lose the payout.
                snap.Objective = q.currentObjective ?? snap.Objective;
            }

            if (q.completed.Value && !_recorded.Contains(q))
            {
                // Mark it completed so if it later leaves the log (player claims
                // via the vanilla journal, a story event yanks it, etc.) the
                // disappearance branch records it as Completed, not Failed.
                snap.LastSeenCompleted = true;

                // A completed quest that still has a reward waiting sits in the
                // log until the reward is collected. Leave it there so the player
                // can claim the gold from the journal instead of dropping it into
                // the Completed tab right away. It gets recorded when it's claimed
                // (JournalContext.ClaimSelected) or when it leaves the log.
                if (q.HasReward()) continue;

                Record(snap, "Completed");
                _recorded.Add(q);
            }
        }

        // Detect quests that vanished from the log between ticks. A friendship-only
        // quest leaves the log inside the same questComplete() call that sets
        // completed.Value, so we may never see it in-log as completed; the removed
        // Quest object still reports completed.Value=true. Genuine failures (deadline
        // expired, story event yanked it) never complete, so they stay Failed.
        // Ignored quests are skipped (the journal's Cancel button told us so).
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
