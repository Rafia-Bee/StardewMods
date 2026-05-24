using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using StardewValley;
using StardewValley.Quests;

namespace QuestJournal.Menu;

public sealed class JournalContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TabRow> Tabs { get; } = new();
    public ObservableCollection<QuestRow> Quests { get; } = new();

    private QuestRow? _selectedQuest;
    public QuestRow? SelectedQuest
    {
        get => _selectedQuest;
        private set => SetField(ref _selectedQuest, value);
    }

    public bool HasSelection => _selectedQuest != null;

    public bool IsEmpty => Quests.Count == 0;

    private string _activeTabId = TabActive;
    private List<QuestEntry> _all = new();

    private const string TabActive = "active";
    private const string TabCompleted = "completed";
    private const string TabAll = "all";

    public JournalContext()
    {
        Tabs.Add(new TabRow(TabActive, "Active", id => SelectTab(id)));
        Tabs.Add(new TabRow(TabCompleted, "Completed", id => SelectTab(id)));
        Tabs.Add(new TabRow(TabAll, "All", id => SelectTab(id)));
        UpdateTabSelection();
    }

    public void Refresh()
    {
        _all.Clear();
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null) continue;
            _all.Add(new QuestEntry(i, q));
        }
        ReapplyFilter(keepSelectionIfPossible: true);
    }

    public void SelectTab(string id)
    {
        if (_activeTabId == id) return;
        _activeTabId = id;
        UpdateTabSelection();
        ReapplyFilter(keepSelectionIfPossible: false);
    }

    public void SelectQuest(int index)
    {
        var match = FindRow(index);
        if (match == null) return;
        if (SelectedQuest != null)
            SelectedQuest.IsSelected = false;
        match.IsSelected = true;
        SelectedQuest = match;
        Raise(nameof(HasSelection));
    }

    private void ReapplyFilter(bool keepSelectionIfPossible)
    {
        int? keepIndex = keepSelectionIfPossible ? SelectedQuest?.Index : null;
        Quests.Clear();
        foreach (var e in _all)
        {
            if (!Matches(e, _activeTabId)) continue;
            Quests.Add(BuildRow(e));
        }
        if (keepIndex.HasValue)
        {
            var keep = FindRow(keepIndex.Value);
            if (keep != null)
            {
                keep.IsSelected = true;
                SelectedQuest = keep;
                Raise(nameof(HasSelection));
                return;
            }
        }
        // Auto-select the first row when the filter changes so the detail
        // panel isn't blank.
        if (Quests.Count > 0)
        {
            Quests[0].IsSelected = true;
            SelectedQuest = Quests[0];
        }
        else
        {
            SelectedQuest = null;
        }
        Raise(nameof(HasSelection));
        Raise(nameof(IsEmpty));
    }

    private static bool Matches(QuestEntry e, string tab) => tab switch
    {
        TabActive => !e.Quest.completed.Value,
        TabCompleted => e.Quest.completed.Value,
        TabAll => true,
        _ => true
    };

    private QuestRow BuildRow(QuestEntry e)
    {
        var q = e.Quest;
        return new QuestRow(
            index: e.Index,
            title: q.questTitle ?? string.Empty,
            description: q.questDescription ?? string.Empty,
            objective: q.currentObjective ?? string.Empty,
            rewardSummary: BuildVanillaRewardSummary(q),
            giverDisplay: ResolveGiverDisplay(q),
            daysLeftDisplay: BuildDaysLeftDisplay(q),
            sourceDisplay: ResolveSourceDisplay(q),
            warpTarget: ResolveGiverNpcName(q),
            isCompleted: q.completed.Value,
            onSelect: idx => SelectQuest(idx));
    }

    private QuestRow? FindRow(int index)
    {
        foreach (var r in Quests)
            if (r.Index == index) return r;
        return null;
    }

    private void UpdateTabSelection()
    {
        foreach (var t in Tabs)
            t.IsActive = (t.Id == _activeTabId);
    }

    private static string BuildVanillaRewardSummary(Quest q)
    {
        // MQF-aware itemisation lands in step 5. For now expose at least the
        // gold reward so the panel isn't blank for vanilla quests.
        int gold = q.moneyReward.Value;
        if (gold > 0) return $"{gold}g";
        return string.IsNullOrEmpty(q.rewardDescription.Value) ? "(none)" : q.rewardDescription.Value;
    }

    private static string BuildDaysLeftDisplay(Quest q)
    {
        int d = q.daysLeft.Value;
        if (d <= 0) return "No deadline";
        if (d == 1) return "1 day left";
        return $"{d} days left";
    }

    private static string ResolveGiverDisplay(Quest q)
    {
        string? name = ResolveGiverNpcName(q);
        return string.IsNullOrEmpty(name) ? "Unknown" : name!;
    }

    private static string? ResolveGiverNpcName(Quest q) => q switch
    {
        ItemDeliveryQuest idq => string.IsNullOrEmpty(idq.target.Value) ? null : idq.target.Value,
        SlayMonsterQuest smq when !string.IsNullOrEmpty(smq.target.Value) && smq.target.Value != "null" => smq.target.Value,
        _ => null
    };

    private static string ResolveSourceDisplay(Quest q)
    {
        // Real source-mod resolution arrives in step 7 (via MQF API +
        // walking the loaded mod registry). For now: vanilla types -> "Stardew
        // Valley", anything else (e.g. AdventureQuest, modded subclass) ->
        // "Modded".
        return q switch
        {
            ItemDeliveryQuest => "Stardew Valley",
            FishingQuest => "Stardew Valley",
            ResourceCollectionQuest => "Stardew Valley",
            SlayMonsterQuest => "Stardew Valley",
            _ => "Modded"
        };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly record struct QuestEntry(int Index, Quest Quest);
}

public sealed class TabRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Label { get; }
    private readonly System.Action<string> _onActivate;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive == value) return; _isActive = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive))); }
    }

    public TabRow(string id, string label, System.Action<string> onActivate)
    {
        Id = id;
        Label = label;
        _onActivate = onActivate;
    }

    public void Activate() => _onActivate(Id);
}

public sealed class QuestRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; }
    public string Title { get; }
    public string Description { get; }
    public string Objective { get; }
    public string RewardSummary { get; }
    public string GiverDisplay { get; }
    public string DaysLeftDisplay { get; }
    public string SourceDisplay { get; }
    public string? WarpTarget { get; }
    public bool IsCompleted { get; }

    private readonly System.Action<int> _onSelect;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public bool HasWarpTarget => !string.IsNullOrEmpty(WarpTarget);
    public string WarpLabel => HasWarpTarget ? $"Warp to {WarpTarget}" : "No warp target";

    public QuestRow(
        int index,
        string title,
        string description,
        string objective,
        string rewardSummary,
        string giverDisplay,
        string daysLeftDisplay,
        string sourceDisplay,
        string? warpTarget,
        bool isCompleted,
        System.Action<int> onSelect)
    {
        Index = index;
        Title = title;
        Description = description;
        Objective = objective;
        RewardSummary = rewardSummary;
        GiverDisplay = giverDisplay;
        DaysLeftDisplay = daysLeftDisplay;
        SourceDisplay = sourceDisplay;
        WarpTarget = warpTarget;
        IsCompleted = isCompleted;
        _onSelect = onSelect;
    }

    public void Select() => _onSelect(Index);
}
