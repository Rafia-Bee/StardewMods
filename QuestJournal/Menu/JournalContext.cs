using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using QuestJournal.Integrations;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
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
        private set
        {
            if (_selectedQuest == value) return;
            _selectedQuest = value;
            Raise(nameof(SelectedQuest));
            RaiseSelectionDependents();
        }
    }

    // Detail / action panels bind directly to these hoisted props so changes
    // to SelectedQuest are visible to StardewUI. *context={SelectedQuest}
    // didn't reliably re-render reactively, so we flatten.
    public string SelectedTitle => _selectedQuest?.Title ?? string.Empty;
    public string SelectedDescription => _selectedQuest?.Description ?? string.Empty;
    public string SelectedObjective => _selectedQuest?.Objective ?? string.Empty;
    public string SelectedRewardSummary => _selectedQuest?.RewardSummary ?? string.Empty;
    public string SelectedGiverDisplay => _selectedQuest?.GiverDisplay ?? string.Empty;
    public string SelectedDaysLeftDisplay => _selectedQuest?.DaysLeftDisplay ?? string.Empty;
    public string SelectedSourceDisplay => _selectedQuest?.SourceDisplay ?? string.Empty;
    public string SelectedWarpLabel => _selectedQuest?.WarpLabel ?? string.Empty;
    public bool SelectedIsCompleted => _selectedQuest?.IsCompleted == true;
    public bool SelectedShowActions => _selectedQuest != null && !_selectedQuest.IsCompleted && _selectedQuest.Quest != null;
    public bool SelectedShowComplete => SelectedShowActions;
    public bool SelectedShowCancel => _selectedQuest != null && _selectedQuest.CanCancel;
    public bool SelectedShowPostpone => _selectedQuest != null && _selectedQuest.CanPostpone;

    public bool HasSelection => _selectedQuest != null;
    public bool IsEmpty => Quests.Count == 0;

    private string _activeTabId = TabActive;
    private List<QuestRow> _activeRows = new();
    private List<QuestRow> _historyRows = new();

    private readonly IViewEngine? _viewEngine;
    private readonly IModHelper _helper;
    private readonly string _viewPrefix;

    private const string TabActive = "active";
    private const string TabCompleted = "completed";
    private const string TabAll = "all";

    public JournalContext(IModHelper helper, IViewEngine? viewEngine, string viewPrefix)
    {
        _helper = helper;
        _viewEngine = viewEngine;
        _viewPrefix = viewPrefix;
        Tabs.Add(new TabRow(TabActive, "Active", id => SelectTab(id)));
        Tabs.Add(new TabRow(TabCompleted, "Completed", id => SelectTab(id)));
        Tabs.Add(new TabRow(TabAll, "All", id => SelectTab(id)));
        UpdateTabSelection();
    }

    public void Refresh()
    {
        _activeRows.Clear();
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value) continue;
            _activeRows.Add(BuildActiveRow(q));
        }

        _historyRows.Clear();
        var history = CompletedQuestStore.Load();
        // Newest first so the Completed tab reads top-down chronologically.
        for (int i = history.Count - 1; i >= 0; i--)
        {
            _historyRows.Add(BuildHistoryRow(history[i]));
        }

        ReapplyFilter();
    }

    public void SelectTab(string id)
    {
        if (_activeTabId == id) return;
        _activeTabId = id;
        UpdateTabSelection();
        ReapplyFilter();
    }

    public void SelectRow(QuestRow row)
    {
        if (SelectedQuest != null)
            SelectedQuest.IsSelected = false;
        row.IsSelected = true;
        SelectedQuest = row;
    }

    public void CompleteSelected()
    {
        if (_selectedQuest == null || _selectedQuest.IsCompleted) return;
        var quest = _selectedQuest.Quest;
        if (quest == null) return;

        // Snapshot BEFORE questComplete: vanilla auto-removes the quest from
        // player.questLog if it has no money + no rewardDescription, so we'd
        // lose the chance to read its state afterward.
        CompletedQuestStore.Add(BuildRecordFrom(_selectedQuest));

        quest.questComplete();

        // Vanilla questComplete doesn't pay moneyReward (the journal's claim
        // button does in vanilla flow). Pay it manually + zero it.
        int money = quest.GetMoneyReward();
        if (money > 0)
        {
            Game1.player.Money += money;
            quest.moneyReward.Value = 0;
        }

        // Always remove from log so vanilla journal stops showing a stale
        // empty-claim row, even if vanilla questComplete already removed it.
        Game1.player.questLog.Remove(quest);
        Refresh();
    }

    public void RequestCancelSelected()
    {
        if (_selectedQuest == null) return;
        var quest = _selectedQuest.Quest;
        if (quest == null || quest.completed.Value || !quest.canBeCancelled.Value) return;

        var savedMenu = Game1.activeClickableMenu;
        var message = _helper.Translation.Get("journal.cancel.confirm")
            .Default("Cancel this quest?").ToString();
        var dialog = new ConfirmationDialog(
            message,
            _ =>
            {
                Game1.player.questLog.Remove(quest);
                if (savedMenu != null)
                    Game1.activeClickableMenu = savedMenu;
                Refresh();
            },
            _ =>
            {
                if (savedMenu != null)
                    Game1.activeClickableMenu = savedMenu;
            });
        Game1.activeClickableMenu = dialog;
    }

    public void PostponeSelected()
    {
        if (_selectedQuest == null) return;
        var quest = _selectedQuest.Quest;
        if (quest == null || quest.completed.Value) return;
        if (quest.daysLeft.Value <= 0) return;
        quest.daysLeft.Value += 7;
        Refresh();
    }

    public void ShowDetailsSelected()
    {
        if (_selectedQuest == null || _viewEngine == null) return;
        var detailsCtx = new QuestDetailsPopupContext(
            _selectedQuest.Title,
            _selectedQuest.Description);
        var popup = _viewEngine.CreateMenuFromAsset($"{_viewPrefix}/quest_details", detailsCtx);
        if (popup != null)
            Game1.activeClickableMenu?.SetChildMenu(popup);
    }

    public void PinSelected() { }
    public void WarpSelected() { }

    private void ReapplyFilter()
    {
        Quests.Clear();
        switch (_activeTabId)
        {
            case TabActive:
                foreach (var r in _activeRows) Quests.Add(r);
                break;
            case TabCompleted:
                foreach (var r in _historyRows) Quests.Add(r);
                break;
            case TabAll:
                foreach (var r in _activeRows) Quests.Add(r);
                foreach (var r in _historyRows) Quests.Add(r);
                break;
        }
        // Auto-select the first row so the detail panel isn't blank when the
        // tab has rows. Drop selection when the tab is empty.
        if (Quests.Count > 0)
        {
            Quests[0].IsSelected = true;
            SelectedQuest = Quests[0];
        }
        else
        {
            SelectedQuest = null;
        }
        Raise(nameof(IsEmpty));
    }

    private QuestRow BuildActiveRow(Quest q)
    {
        return new QuestRow(
            title: q.questTitle ?? string.Empty,
            description: q.questDescription ?? string.Empty,
            objective: q.currentObjective ?? string.Empty,
            rewardSummary: BuildVanillaRewardSummary(q),
            giverDisplay: ResolveGiverDisplay(q),
            daysLeftDisplay: BuildDaysLeftDisplay(q),
            sourceDisplay: ResolveSourceDisplay(q),
            warpTarget: ResolveGiverNpcName(q),
            isCompleted: false,
            canCancel: q.canBeCancelled.Value,
            canPostpone: q.daysLeft.Value > 0,
            quest: q,
            host: this);
    }

    private QuestRow BuildHistoryRow(CompletedQuestRecord r)
    {
        return new QuestRow(
            title: r.Title,
            description: r.Description,
            objective: r.Objective,
            rewardSummary: r.RewardSummary,
            giverDisplay: string.IsNullOrEmpty(r.Giver) ? "Unknown" : r.Giver,
            daysLeftDisplay: "Completed",
            sourceDisplay: string.IsNullOrEmpty(r.Source) ? "Unknown" : r.Source,
            warpTarget: null,
            isCompleted: true,
            canCancel: false,
            canPostpone: false,
            quest: null,
            host: this);
    }

    private static CompletedQuestRecord BuildRecordFrom(QuestRow row)
    {
        return new CompletedQuestRecord
        {
            Title = row.Title,
            Description = row.Description,
            Objective = row.Objective,
            RewardSummary = row.RewardSummary,
            Giver = row.GiverDisplay,
            Source = row.SourceDisplay,
            CompletedOnTotalDays = Game1.Date?.TotalDays ?? 0
        };
    }

    private void UpdateTabSelection()
    {
        foreach (var t in Tabs)
            t.IsActive = (t.Id == _activeTabId);
    }

    private static string BuildVanillaRewardSummary(Quest q)
    {
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
        return q switch
        {
            ItemDeliveryQuest => "Stardew Valley",
            FishingQuest => "Stardew Valley",
            ResourceCollectionQuest => "Stardew Valley",
            SlayMonsterQuest => "Stardew Valley",
            _ => "Modded"
        };
    }

    private void RaiseSelectionDependents()
    {
        Raise(nameof(SelectedTitle));
        Raise(nameof(SelectedDescription));
        Raise(nameof(SelectedObjective));
        Raise(nameof(SelectedRewardSummary));
        Raise(nameof(SelectedGiverDisplay));
        Raise(nameof(SelectedDaysLeftDisplay));
        Raise(nameof(SelectedSourceDisplay));
        Raise(nameof(SelectedWarpLabel));
        Raise(nameof(SelectedIsCompleted));
        Raise(nameof(SelectedShowActions));
        Raise(nameof(SelectedShowComplete));
        Raise(nameof(SelectedShowCancel));
        Raise(nameof(SelectedShowPostpone));
        Raise(nameof(HasSelection));
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

    public string Title { get; }
    public string Description { get; }
    public string Objective { get; }
    public string RewardSummary { get; }
    public string GiverDisplay { get; }
    public string DaysLeftDisplay { get; }
    public string SourceDisplay { get; }
    public string? WarpTarget { get; }
    public bool IsCompleted { get; }
    public bool CanCancel { get; }
    public bool CanPostpone { get; }
    // Live quest reference for active rows. Null for historical rows
    // (snapshots from CompletedQuestStore).
    public Quest? Quest { get; }

    private readonly JournalContext _host;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public bool HasWarpTarget => !string.IsNullOrEmpty(WarpTarget);
    public string WarpLabel => HasWarpTarget ? $"Warp to {WarpTarget}" : "No warp target";

    public QuestRow(
        string title,
        string description,
        string objective,
        string rewardSummary,
        string giverDisplay,
        string daysLeftDisplay,
        string sourceDisplay,
        string? warpTarget,
        bool isCompleted,
        bool canCancel,
        bool canPostpone,
        Quest? quest,
        JournalContext host)
    {
        Title = title;
        Description = description;
        Objective = objective;
        RewardSummary = rewardSummary;
        GiverDisplay = giverDisplay;
        DaysLeftDisplay = daysLeftDisplay;
        SourceDisplay = sourceDisplay;
        WarpTarget = warpTarget;
        IsCompleted = isCompleted;
        CanCancel = canCancel;
        CanPostpone = canPostpone;
        Quest = quest;
        _host = host;
    }

    public void Select() => _host.SelectRow(this);
}

public sealed class QuestDetailsPopupContext
{
    public string Title { get; }
    public string Description { get; }
    public QuestDetailsPopupContext(string title, string description)
    {
        Title = title;
        Description = description;
    }
}
