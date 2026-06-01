using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.Xna.Framework;
using QuestJournal.Api;
using QuestJournal.Cheats;
using QuestJournal.Hud;
using QuestJournal.Integrations;
using QuestJournal.Warp;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;
using StardewValley.ItemTypeDefinitions;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;

namespace QuestJournal.Menu;

// How the quests list is ordered. Saved to config (by name) so it persists and
// applies to every tab. The order here is the order shown in the dropdown.
public enum SortMode
{
    Deadline,
    Alphabetical,
    Giver,
    Source,
    Category
}

public sealed class JournalContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TabRow> Tabs { get; } = new();
    // Tabs that don't fit on the bottom row, packed into rows that stack above
    // it (see RebuildTabRows). The bottom row itself is BottomRowTabs.
    public ObservableCollection<TabRowGroup> OverflowRowGroups { get; } = new();
    // The first tabs, which share the always-visible bottom row with the "+"
    // and "Edit tabs" controls.
    public ObservableCollection<TabRow> BottomRowTabs { get; } = new();
    public ObservableCollection<QuestRow> Quests { get; } = new();
    public ObservableCollection<RewardLineRow> SelectedRewards { get; } = new();
    public ObservableCollection<AdventureStepRow> SelectedSteps { get; } = new();

    private QuestRow? _selectedQuest;
    public QuestRow? SelectedQuest
    {
        get => _selectedQuest;
        private set
        {
            if (_selectedQuest == value) return;
            _selectedQuest = value;
            Raise(nameof(SelectedQuest));
            RebuildSelectedRewards();
            RebuildSelectedSteps();
            RaiseSelectionDependents();
        }
    }

    // Detail / action panels bind directly to these hoisted props so changes
    // to SelectedQuest are visible to StardewUI. *context={SelectedQuest}
    // didn't reliably re-render reactively, so we flatten.
    public string SelectedTitle => _selectedQuest?.Title ?? string.Empty;
    public string SelectedDescription => _selectedQuest?.Description ?? string.Empty;
    public string SelectedObjective => _selectedQuest?.Objective ?? string.Empty;
    public string SelectedGiverDisplay => _selectedQuest?.GiverDisplay ?? string.Empty;
    public string SelectedDaysLeftDisplay => _selectedQuest?.DaysLeftDisplay ?? string.Empty;
    public string SelectedSourceDisplay => _selectedQuest?.SourceDisplay ?? string.Empty;
    // Single NPC reads "Warp to X"; multiple touches open the dropdown, so the
    // button just says "Warp...". Empty when nothing's selected or warpable.
    public string SelectedWarpLabel
    {
        get
        {
            var targets = _selectedQuest?.WarpTargets;
            if (targets == null || targets.Count == 0) return string.Empty;
            if (targets.Count == 1)
                return _helper.Translation.Get("journal.action.warpto", new { npc = targets[0].DisplayName })
                    .Default($"Warp to {targets[0].DisplayName}").ToString();
            return _helper.Translation.Get("journal.action.warpmany").Default("Warp...").ToString();
        }
    }
    public bool SelectedIsCompleted => _selectedQuest?.IsCompleted == true;
    public bool SelectedShowActions => _selectedQuest != null && !_selectedQuest.IsCompleted && _selectedQuest.Quest != null;
    // Complete pays the reward and clears the quest without doing the objective,
    // so it's a cheat too. Gated behind AllowCompleteCheat (default off).
    public bool SelectedShowComplete => SelectedShowActions && ModEntry.Config.AllowCompleteCheat;
    // A finished quest or special order whose gold is still waiting. Shows a Claim
    // button that pays the gold and clears it, same as the vanilla journal. Only
    // claimable rows set CanClaim, and they always carry a live quest or order.
    public bool SelectedCanClaim => _selectedQuest?.CanClaim == true;
    public bool SelectedShowCancel => _selectedQuest != null && _selectedQuest.CanCancel;
    public bool SelectedShowPostpone => _selectedQuest != null && _selectedQuest.CanPostpone;
    // Details / Pin / Warp work for live quests and special orders, not history
    // rows. Complete / Postpone / Cancel stay quest-only.
    public bool SelectedShowDetails => _selectedQuest != null && !_selectedQuest.IsCompleted
        && (_selectedQuest.Quest != null || _selectedQuest.SpecialOrder != null);
    public bool SelectedShowPin => SelectedShowDetails;
    // Warp is gated behind AllowWarpCheat (default off) and needs at least one
    // resolvable NPC to warp to, else the button has nothing to do.
    public bool SelectedShowWarp => SelectedShowDetails && ModEntry.Config.AllowWarpCheat
        && _selectedQuest != null && _selectedQuest.WarpTargets.Count > 0;
    public bool SelectedIsPinned =>
        (_selectedQuest?.Quest is Quest q && PinnedObjectivesStore.IsPinned(q))
        || (_selectedQuest?.SpecialOrder is SpecialOrder so && PinnedObjectivesStore.IsPinned(so));
    public string SelectedPinLabel => _helper.Translation
        .Get(SelectedIsPinned ? "journal.action.unpin" : "journal.action.pin")
        .Default(SelectedIsPinned ? "Unpin" : "Pin").ToString();
    // Item helper (cheat). Spawns the target item for item-style quests, or
    // advances the catch counter for fishing quests. Hidden unless AllowItemCheats
    // is on and the live quest actually has something the helper can do.
    public bool SelectedShowItemHelper => _selectedQuest?.Quest is Quest q
        && ModEntry.Config.AllowItemCheats
        && AdaptiveItemSpawner.CanHelp(q, _mqfApi, _helper, out _);
    public string SelectedItemHelperLabel
    {
        get
        {
            if (_selectedQuest?.Quest is Quest q && ModEntry.Config.AllowItemCheats
                && AdaptiveItemSpawner.CanHelp(q, _mqfApi, _helper, out string label))
                return label;
            return string.Empty;
        }
    }
    // SML swaps between the single Objective line and the multi-step list
    // based on these. A stepped Adventure quest renders the step list and
    // hides the objective; everything else keeps the single objective.
    public bool SelectedHasSteps => _selectedQuest?.AdventureSteps.Count > 0;
    public bool SelectedShowObjective => !SelectedHasSteps && !string.IsNullOrEmpty(SelectedObjective);

    public bool HasSelection => _selectedQuest != null;
    public bool NoSelection => _selectedQuest == null;
    public bool IsEmpty => Quests.Count == 0;

    // Free-text search box in the top row. Filters the current tab's rows by
    // title (case-insensitive substring). Two-way bound so typing re-filters
    // live; cleared text shows the whole tab again.
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            string v = value ?? string.Empty;
            if (_searchText == v) return;
            _searchText = v;
            Raise(nameof(SearchText));
            ReapplyFilter();
        }
    }

    public void ClearSearch() => SearchText = string.Empty;

    // Sort order for the quests list. Persisted to config and applied to every
    // tab. SortKinds[i] is the mode for SortOptions[i] (the labels the dropdown
    // shows), so the selected label maps back to a mode by index.
    private static readonly SortMode[] SortKinds =
    {
        SortMode.Deadline, SortMode.Alphabetical, SortMode.Giver, SortMode.Source, SortMode.Category
    };
    private SortMode _sortMode = SortMode.Deadline;
    public List<string> SortOptions { get; } = new();

    public string SelectedSortLabel
    {
        get
        {
            int i = System.Array.IndexOf(SortKinds, _sortMode);
            if (i >= 0 && i < SortOptions.Count) return SortOptions[i];
            return SortOptions.Count > 0 ? SortOptions[0] : string.Empty;
        }
        set
        {
            int i = SortOptions.IndexOf(value ?? string.Empty);
            if (i < 0) return;
            var mode = SortKinds[i];
            if (_sortMode == mode) return;
            _sortMode = mode;
            ModEntry.Config.QuestSort = mode.ToString();
            _helper.WriteConfig(ModEntry.Config);
            Raise(nameof(SelectedSortLabel));
            ReapplyFilter();
        }
    }

    private static SortMode ParseSortMode(string? name)
        => System.Enum.TryParse<SortMode>(name, ignoreCase: true, out var m) ? m : SortMode.Deadline;

    // Edit mode (mirrors Better Crafting's category editing): off by default and
    // tabs just switch. When on, clicking a custom tab opens its editor to
    // rename / re-filter / delete it. Built-in tabs always just switch.
    private bool _editMode;
    public bool EditMode
    {
        get => _editMode;
        private set
        {
            if (_editMode == value) return;
            _editMode = value;
            Raise(nameof(EditMode));
            Raise(nameof(EditButtonLabel));
            // The edit tab lives on the rail and its label flips with the mode,
            // which changes its width, so re-pack the rows.
            if (_editTab != null)
            {
                _editTab.Label = EditButtonLabel;
                RebuildTabRows();
            }
        }
    }
    public string EditButtonLabel => _editMode
        ? _helper.Translation.Get("journal.tab.editdone").Default("Done").ToString()
        : _helper.Translation.Get("journal.tab.edit").Default("Edit tabs").ToString();
    public void ToggleEditMode() => EditMode = !_editMode;

    // Shared fallback label for a missing giver / source.
    private string UnknownLabel => _helper.Translation.Get("journal.unknown").Default("Unknown").ToString();

    // Static translation helpers for the Special Order reward builders below,
    // which stay static (no instance state). Falls back to the English literal
    // before the mod entry is ready.
    private static string T(string key, string fallback)
        => ModEntry.Instance?.Helper?.Translation.Get(key).Default(fallback).ToString() ?? fallback;

    private static string T(string key, object tokens, string fallback)
        => ModEntry.Instance?.Helper?.Translation.Get(key, tokens).Default(fallback).ToString() ?? fallback;

    // Section-heading colour for the detail panel, themed via JournalTheme.
    public Color HeaderColor => JournalTheme.HeaderColor;

    // Whole-journal zoom, done as a real layout resize (not a transform). Every
    // structural dimension is base * scale, so the actual layout boxes grow and
    // StardewUI's hit-testing/centring stays correct. A transform was tried
    // first but only scaled the pixels, leaving clicks mapped to the 1x layout.
    // Font size is fixed by the engine, so text stays native size; the boxes
    // just get roomier. Clamped so a bad config value can't break the journal.
    private float Scale
    {
        get
        {
            float s = ModEntry.Config.JournalScale;
            if (s < 0.7f) return 0.7f;
            if (s > 1.5f) return 1.5f;
            return s;
        }
    }

    private string Px(float baseValue)
        => ((int)System.Math.Round(baseValue * Scale)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "px";

    // Heights stretch so the panels fill the frame instead of leaving dead space
    // below them, and they stay filled at any JournalScale. Only the widths are
    // fixed per panel.
    public string RootLayout => $"{Px(1100)} {Px(720)}";
    public string PanelRowLayout => "content stretch";
    public string ListPanelLayout => $"{Px(240)} stretch";
    public string DetailPanelLayout => $"{Px(484)} stretch";
    public string ActionPanelLayout => $"{Px(236)} stretch";
    public string ActionLaneLayout => "stretch content";

    // Drag-to-move. GetJournalTopLeft is the screen-centered position plus a
    // persisted offset; ModEntry pushes it onto the menu every tick (StardewUI's
    // PositionSelector only re-evaluates on re-measure, which a drag doesn't
    // trigger). The drag itself is handled in ModEntry via SMAPI input, which
    // calls SetJournalTopLeft as the cursor moves.
    private Point _journalOffset;

    public int JournalFrameWidth => (int)System.Math.Round(1100 * Scale);
    public int JournalFrameHeight => (int)System.Math.Round(720 * Scale);

    private Point CenteredBase()
        => new Point(
            (Game1.uiViewport.Width - JournalFrameWidth) / 2,
            (Game1.uiViewport.Height - JournalFrameHeight) / 2);

    public Point GetJournalTopLeft()
    {
        var c = CenteredBase();
        return new Point(c.X + _journalOffset.X, c.Y + _journalOffset.Y);
    }

    public void SetJournalTopLeft(Point topLeft)
    {
        var c = CenteredBase();
        _journalOffset = new Point(topLeft.X - c.X, topLeft.Y - c.Y);
    }

    public void PersistJournalOffset()
    {
        ModEntry.Config.JournalOffsetX = _journalOffset.X;
        ModEntry.Config.JournalOffsetY = _journalOffset.Y;
        _helper.WriteConfig(ModEntry.Config);
    }

    // The "+" and "Edit tabs" controls are bound directly (not via a repeat) so
    // they can be pinned to the right edge of the bottom row.
    public TabRow? AddTab => _addTab;
    public TabRow? EditTab => _editTab;

    // Left margin of the controls float, which positions the "+"/"Edit" icons at
    // a fixed spot on the right (independent of how many tabs exist or how wide
    // they render). Recomputed by RebuildTabRows.
    private string _controlsLeftMargin = "0, 0, 0, -8";
    public string ControlsLeftMargin
    {
        get => _controlsLeftMargin;
        private set { if (_controlsLeftMargin == value) return; _controlsLeftMargin = value; Raise(nameof(ControlsLeftMargin)); }
    }

    // Repaint an open journal after the theme asset is repatched.
    public void RefreshTheme()
    {
        Raise(nameof(HeaderColor));
        foreach (var r in Quests)
            r.RaiseThemeColors();
    }

    private string _activeTabId = TabActive;
    // The "+" and "Edit tabs" controls ride the rail as trailing tabs so they
    // pack and stack with everything else. They're not in Tabs (which is the
    // selectable model); RebuildTabRows appends them after the real tabs.
    private TabRow? _addTab;
    private TabRow? _editTab;
    private List<QuestRow> _activeRows = new();
    // Quests that finished by playing them but still have a reward waiting to be
    // collected. They sit at the top of the Active tab with a Claim button so the
    // gold can be taken from here instead of the vanilla journal.
    private List<QuestRow> _claimableRows = new();
    // Same idea for completed Special Orders whose gold is still waiting. Vanilla
    // leaves the order in the team list until its reward box is clicked in the
    // vanilla log. These ride at the top of the Special Orders tab.
    private List<QuestRow> _claimableOrderRows = new();
    private List<QuestRow> _historyRows = new();
    private List<QuestRow> _specialOrderRows = new();

    private readonly IViewEngine? _viewEngine;
    private readonly IMoreQuestsApi? _mqfApi;
    private readonly IModHelper _helper;
    private readonly string _viewPrefix;
    private readonly CompletionWatcher? _completionWatcher;

    private const string TabActive = "active";
    private const string TabSpecial = "special";
    private const string TabCompleted = "completed";
    private const string TabAll = "all";

    public JournalContext(IModHelper helper, IViewEngine? viewEngine, IMoreQuestsApi? mqfApi, string viewPrefix, CompletionWatcher? completionWatcher)
    {
        _helper = helper;
        _viewEngine = viewEngine;
        _mqfApi = mqfApi;
        _viewPrefix = viewPrefix;
        _completionWatcher = completionWatcher;
        _journalOffset = new Point(ModEntry.Config.JournalOffsetX, ModEntry.Config.JournalOffsetY);
        Tabs.Add(new TabRow(TabActive, helper.Translation.Get("journal.tab.active").Default("Active").ToString(), HandleTabActivate));
        Tabs.Add(new TabRow(TabSpecial, helper.Translation.Get("journal.tab.special").Default("Special Orders").ToString(), HandleTabActivate));
        Tabs.Add(new TabRow(TabCompleted, helper.Translation.Get("journal.tab.completed").Default("Completed").ToString(), HandleTabActivate));
        Tabs.Add(new TabRow(TabAll, helper.Translation.Get("journal.tab.all").Default("All").ToString(), HandleTabActivate));
        _addTab = new TabRow("__add", helper.Translation.Get("journal.tab.new").Default("New tab").ToString(), _ => CreateTab()) { IsAddTab = true };
        _editTab = new TabRow("__edit", EditButtonLabel, _ => ToggleEditMode()) { IsEditTab = true };
        // Same order as SortKinds, so a selected label maps to a mode by index.
        SortOptions.Add(helper.Translation.Get("journal.sort.deadline").Default("Deadline").ToString());
        SortOptions.Add(helper.Translation.Get("journal.sort.alphabetical").Default("Alphabetical (A-Z)").ToString());
        SortOptions.Add(helper.Translation.Get("journal.sort.giver").Default("By giver").ToString());
        SortOptions.Add(helper.Translation.Get("journal.sort.source").Default("By source").ToString());
        SortOptions.Add(helper.Translation.Get("journal.sort.category").Default("By category").ToString());
        _sortMode = ParseSortMode(ModEntry.Config.QuestSort);
        LoadCustomTabs();
    }

    public void Refresh()
    {
        _activeRows.Clear();
        _claimableRows.Clear();
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null) continue;
            if (q.completed.Value)
            {
                // Vanilla removes a finished quest from the log right away unless
                // it has a reward to collect, so a completed quest still sitting
                // here has gold (or a reward note) waiting. Surface it as
                // claimable. Anything else completed is skipped.
                if (q.HasReward())
                    _claimableRows.Add(BuildClaimableRow(q));
                continue;
            }
            _activeRows.Add(BuildActiveRow(q));
        }

        _specialOrderRows.Clear();
        _claimableOrderRows.Clear();
        // Special Orders live in player.team.specialOrders, not questLog, so
        // they need a parallel pass. In-progress orders go in the tab; failed
        // ones are removed by vanilla within a day or two and don't need a
        // history bucket (yet). A completed order whose gold is still waiting
        // (HasMoneyReward) becomes a claimable row, same as a finished quest.
        var orders = Game1.player?.team?.specialOrders;
        if (orders != null)
        {
            foreach (var so in orders)
            {
                if (so == null) continue;
                if (so.questState.Value == SpecialOrderStatus.InProgress)
                    _specialOrderRows.Add(BuildSpecialOrderRow(so));
                else if (so.HasMoneyReward())
                    _claimableOrderRows.Add(BuildClaimableOrderRow(so));
            }
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
        // Guard against a click that targets a tab no longer in the list (e.g.
        // one just removed via the editor). No matching row, do nothing.
        if (FindTab(id) == null) return;
        _activeTabId = id;
        UpdateTabSelection();
        ReapplyFilter();
    }

    // Controller tab cycling. The tab rail renders in a floating element, which
    // StardewUI excludes from gamepad focus, so the stick can't reach the tabs.
    // The shoulder/trigger buttons cycle through them instead (matching how the
    // vanilla menus switch tabs on a controller). Cycles over Tabs, which holds
    // only the selectable tabs (the +/Edit controls aren't in it), wrapping at
    // both ends.
    public void NextTab() => CycleTab(1);
    public void PrevTab() => CycleTab(-1);

    private void CycleTab(int step)
    {
        if (Tabs.Count == 0) return;
        int current = 0;
        for (int i = 0; i < Tabs.Count; i++)
            if (Tabs[i].Id == _activeTabId) { current = i; break; }
        int next = ((current + step) % Tabs.Count + Tabs.Count) % Tabs.Count;
        SelectTab(Tabs[next].Id);
    }

    // Controller editor shortcut. The "+" / "Edit tabs" controls float above the
    // frame, so a gamepad can't reach them to enter edit mode and click a custom
    // tab. Instead, Y opens the editor straight from the selected tab when that
    // tab is a custom one. Built-in tabs have no editor, so this no-ops on them.
    public bool CanEditActiveTab => FindTab(_activeTabId)?.IsCustom == true;

    public void EditActiveTab()
    {
        var tab = FindTab(_activeTabId);
        if (tab?.IsCustom == true)
            OpenTabEditor(tab);
    }

    public void SelectRow(QuestRow row)
    {
        var previous = SelectedQuest;
        if (previous == row) return;
        if (previous != null)
            previous.IsSelected = false;
        row.IsSelected = true;
        SelectedQuest = row;
    }

    // Used by the HUD click-through to select the quest/order behind a pin key.
    public bool SelectQuestByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var match = FindRowByKey(key);
        if (match == null)
        {
            // Quests live in the Active tab, special orders ("so:") in Special.
            SelectTab(key.StartsWith("so:", System.StringComparison.Ordinal) ? TabSpecial : TabActive);
            match = FindRowByKey(key);
        }
        if (match == null) return false;
        SelectRow(match);
        return true;
    }

    private QuestRow? FindRowByKey(string key)
    {
        foreach (var r in Quests)
        {
            if (r.Quest is Quest q && PinnedObjectivesStore.KeyFor(q) == key)
                return r;
            if (r.SpecialOrder is SpecialOrder so && PinnedObjectivesStore.KeyFor(so) == key)
                return r;
        }
        return null;
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
        // Tell the watcher we already handled this one so it doesn't write a
        // duplicate row when it later sees the completed.Value flip.
        _completionWatcher?.MarkRecorded(quest);

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
        PinnedObjectivesStore.Unpin(quest);
        Refresh();
    }

    // Collects the reward for a quest that was finished by playing it but whose
    // gold is still waiting in the log. Pays the money, tidies the quest out of
    // the log, and moves it into the Completed tab. This is the natural-completion
    // counterpart to the vanilla journal's reward-collect click.
    public void ClaimSelected()
    {
        if (_selectedQuest == null || !_selectedQuest.CanClaim) return;

        if (_selectedQuest.Quest is Quest quest)
        {
            if (!quest.completed.Value) return;
            // Record the history row now and tell the watcher we handled it, so
            // it doesn't write a second copy when the quest leaves the log.
            CompletedQuestStore.Add(BuildRecordFrom(_selectedQuest));
            _completionWatcher?.MarkRecorded(quest);

            PayClaim(quest.GetMoneyReward());
            // Vanilla's own "reward taken" call: zeros the money and marks the
            // quest for removal. We remove it right after either way.
            quest.OnMoneyRewardClaimed();
            Game1.player.questLog.Remove(quest);
            PinnedObjectivesStore.Unpin(quest);
        }
        else if (_selectedQuest.SpecialOrder is SpecialOrder order)
        {
            if (!order.HasMoneyReward()) return;
            PayClaim(order.GetMoneyReward());
            // Drops us from the order's participants and flags it for removal,
            // exactly like clicking its reward box in the vanilla log. The game
            // removes it from the team list on its next Update.
            order.OnMoneyRewardClaimed();
            PinnedObjectivesStore.Unpin(order);
        }
        else return;

        Refresh();
    }

    // Pays a claimed gold reward with the same coin sound and popup feedback for
    // quests and special orders.
    private void PayClaim(int money)
    {
        if (money <= 0) return;
        Game1.player.Money += money;
        Game1.playSound("money");
        Game1.addHUDMessage(HUDMessage.ForCornerTextbox(
            _helper.Translation.Get("journal.claim.received", new { amount = money })
                .Default($"Received {money}g").ToString()));
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
                // The watcher would otherwise see the disappearance and
                // record this as Failed. The player chose to drop it,
                // so mark it ignored before the actual removal.
                _completionWatcher?.MarkIgnore(quest);
                Game1.player.questLog.Remove(quest);
                PinnedObjectivesStore.Unpin(quest);
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

    // Item helper (cheat, gated behind AllowItemCheats). Spawns the quest's
    // target item into the bag, or for a fishing quest advances the catch
    // counter by one. Rebuilds the list so progress/objective reflect the
    // change, then keeps the same quest selected so repeat clicks stay put.
    public void ItemHelperSelected()
    {
        if (!ModEntry.Config.AllowItemCheats) return;
        var quest = _selectedQuest?.Quest;
        if (quest == null) return;
        string message = AdaptiveItemSpawner.Apply(quest, _mqfApi, _helper);
        if (!string.IsNullOrEmpty(message))
            Game1.addHUDMessage(HUDMessage.ForCornerTextbox(message));
        Refresh();
        ReselectQuest(quest);
    }

    private void ReselectQuest(Quest quest)
    {
        foreach (var r in Quests)
            if (ReferenceEquals(r.Quest, quest)) { SelectRow(r); return; }
    }

    public void ShowDetailsSelected()
    {
        if (_selectedQuest == null || _viewEngine == null) return;
        var detailsCtx = new QuestDetailsPopupContext(
            _selectedQuest.Title,
            _selectedQuest.Description);
        var popupController = _viewEngine.CreateMenuControllerFromAsset($"{_viewPrefix}/quest_details", detailsCtx);
        if (popupController != null)
        {
            popupController.DimmingAmount = 0f;
            popupController.Closed += () => popupController.Dispose();
            Game1.activeClickableMenu?.SetChildMenu(popupController.Menu);
        }
    }

    // Toggles the HUD pin for the selected quest or special order.
    public void PinSelected()
    {
        if (_selectedQuest?.Quest is Quest q)
            PinnedObjectivesStore.Toggle(q);
        else if (_selectedQuest?.SpecialOrder is SpecialOrder so)
            PinnedObjectivesStore.Toggle(so);
        else
            return;
        Raise(nameof(SelectedIsPinned));
        Raise(nameof(SelectedPinLabel));
    }

    // One NPC warps straight there; more than one opens a picker popup.
    public void WarpSelected()
    {
        var targets = _selectedQuest?.WarpTargets;
        if (targets == null || targets.Count == 0) return;
        if (targets.Count == 1)
        {
            NpcWarpResolver.Warp(targets[0].InternalName);
            return;
        }
        OpenWarpDropdown(targets);
    }

    private void OpenWarpDropdown(IReadOnlyList<WarpNpc> targets)
    {
        if (_viewEngine == null) return;
        string title = _helper.Translation.Get("journal.warp.title").Default("Warp to...").ToString();
        var ctx = new WarpDropdownContext(title);
        foreach (var t in targets)
        {
            string label = _helper.Translation.Get("journal.action.warpto", new { npc = t.DisplayName })
                .Default($"Warp to {t.DisplayName}").ToString();
            ctx.Options.Add(new WarpOptionRow(label, t.InternalName));
        }
        var controller = _viewEngine.CreateMenuControllerFromAsset($"{_viewPrefix}/warp_dropdown", ctx);
        if (controller == null) return;
        controller.DimmingAmount = 0f;
        controller.Closed += () => controller.Dispose();
        Game1.activeClickableMenu?.SetChildMenu(controller.Menu);
    }

    // Tab clicks route through here so edit mode can intercept. Built-in tabs
    // always just switch; a custom tab in edit mode opens its editor instead.
    private void HandleTabActivate(TabRow row)
    {
        if (_editMode && row.IsCustom)
        {
            OpenTabEditor(row);
            return;
        }
        SelectTab(row.Id);
    }

    // Bound to the "+" button. Parameterless so the StarML event binding has an
    // unambiguous target.
    public void CreateTab() => OpenTabEditor(null);

    // Create (existing == null) or edit (existing != null) a custom tab. The
    // same popup handles both; when editing it pre-fills the fields and shows a
    // Delete button. Opened as a child menu so Esc returns to the journal.
    public void OpenTabEditor(TabRow? existing = null)
    {
        if (_viewEngine == null) return;
        var def = existing?.CustomDef;
        var ctx = new CustomTabEditorContext(
            BuildHint(r => r.Category),
            BuildHint(r => r.Kind),
            isEdit: def != null)
        {
            Name = def?.Name ?? string.Empty,
            TitleFilter = def?.TitleFilter ?? string.Empty,
            SourceFilter = def?.SourceFilter ?? string.Empty,
            CategoryFilter = def?.CategoryFilter ?? string.Empty,
            KindFilter = def?.KindFilter ?? string.Empty,
            DeadlineFilter = def?.DeadlineFilter ?? string.Empty
        };
        var controller = _viewEngine.CreateMenuControllerFromAsset($"{_viewPrefix}/custom_tab_editor", ctx);
        if (controller == null) return;
        controller.DimmingAmount = 0f;
        controller.Closed += () => controller.Dispose();
        ctx.Bind(
            onSave: c =>
            {
                if (def != null)
                {
                    // Edit in place: keep the id so selection/persistence stay stable.
                    var list = CustomTabStore.Load();
                    var match = list.Find(t => t.Id == def.Id);
                    if (match != null)
                    {
                        match.Name = NameOrDefault(c.Name);
                        match.TitleFilter = (c.TitleFilter ?? string.Empty).Trim();
                        match.SourceFilter = (c.SourceFilter ?? string.Empty).Trim();
                        match.CategoryFilter = (c.CategoryFilter ?? string.Empty).Trim();
                        match.KindFilter = (c.KindFilter ?? string.Empty).Trim();
                        match.DeadlineFilter = (c.DeadlineFilter ?? string.Empty).Trim();
                        CustomTabStore.Save(list);
                    }
                    LoadCustomTabs();
                    controller.Close();
                    // Force a reapply even if this tab is already active, so the
                    // changed filters take effect right away.
                    _activeTabId = def.Id;
                    UpdateTabSelection();
                    ReapplyFilter();
                }
                else
                {
                    var newDef = new CustomTabDef
                    {
                        Id = System.Guid.NewGuid().ToString("N"),
                        Name = NameOrDefault(c.Name),
                        TitleFilter = (c.TitleFilter ?? string.Empty).Trim(),
                        SourceFilter = (c.SourceFilter ?? string.Empty).Trim(),
                        CategoryFilter = (c.CategoryFilter ?? string.Empty).Trim(),
                        KindFilter = (c.KindFilter ?? string.Empty).Trim(),
                        DeadlineFilter = (c.DeadlineFilter ?? string.Empty).Trim()
                    };
                    CustomTabStore.Add(newDef);
                    LoadCustomTabs();
                    controller.Close();
                    SelectTab(newDef.Id);
                }
            },
            onDelete: () =>
            {
                if (def == null) { controller.Close(); return; }
                bool wasActive = _activeTabId == def.Id;
                CustomTabStore.Remove(def.Id);
                LoadCustomTabs();
                controller.Close();
                if (wasActive)
                {
                    _activeTabId = TabActive;
                    UpdateTabSelection();
                    ReapplyFilter();
                }
            },
            onCancel: () => controller.Close());
        Game1.activeClickableMenu?.SetChildMenu(controller.Menu);
    }

    private static string NameOrDefault(string? name)
        => string.IsNullOrWhiteSpace(name) ? "Tab" : name!.Trim();

    // Drops the existing custom rows (the four built-ins stay at the front) and
    // re-adds them from the store. Called on open and after every add/delete.
    private void LoadCustomTabs()
    {
        for (int i = Tabs.Count - 1; i >= 0; i--)
            if (Tabs[i].IsCustom) Tabs.RemoveAt(i);
        foreach (var def in CustomTabStore.Load())
            Tabs.Add(new TabRow(def.Id, def.Name, HandleTabActivate, def));
        UpdateTabSelection();
        RebuildTabRows();
    }

    // Lays out the rail. The "+" and "Edit tabs" controls live in their own
    // floating element pinned to a fixed spot at the bottom-right (computed from
    // the frame width and control width only, so they never drift as tabs are
    // added). The first tabs share that bottom level; any that don't fit beside
    // the controls wrap to rows that stack above. Tabs are uniform width (widest
    // label, clamped; longer labels truncate with the full name in the tooltip),
    // measured off Game1.smallFont so the packed width matches the draw.
    private void RebuildTabRows()
    {
        OverflowRowGroups.Clear();
        BottomRowTabs.Clear();

        var font = Game1.smallFont;
        const float minContent = 64f;
        const float maxContent = 200f;
        float widest = minContent;
        foreach (var t in Tabs)
        {
            try { widest = System.Math.Max(widest, font.MeasureString(t.Label ?? string.Empty).X); }
            catch { }
        }
        float uniformContent = System.Math.Min(widest, maxContent);

        const float padding = 16f;       // "8, 0" horizontal padding, both sides
        const float interMargin = 8f;    // each tab's right margin
        const int tabHeight = 60;        // fixed so middle-alignment can center content
        int frameWidth = (int)System.Math.Ceiling(uniformContent) + (int)padding;
        string widthLayout = frameWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "px " + tabHeight + "px";

        const string iconLayout = "62px 60px";
        if (_addTab != null) _addTab.WidthLayout = iconLayout;
        if (_editTab != null) _editTab.WidthLayout = iconLayout;

        foreach (var t in Tabs)
        {
            t.WidthLayout = widthLayout;
            t.DisplayLabel = Truncate(t.Label ?? string.Empty, font, uniformContent);
        }

        float footprint = frameWidth + interMargin;
        float outerWidth = 1100f * Scale;

        // The control pair renders ~103px wide (measured in-game at scale 1.0,
        // not the nominal 62px each) and is pinned 36px from the frame's right.
        const float controlsRenderWidth = 103f;
        const float rightInset = 36f;
        const float railLeft = 36f;      // must match the rail float's left margin
        const float gapNudge = 5f;       // tuned in-game: a touch more space after the last tab
        float controlsLeft = outerWidth - rightInset - controlsRenderWidth + gapNudge;
        ControlsLeftMargin = ((int)controlsLeft).ToString(System.Globalization.CultureInfo.InvariantCulture) + ", 0, 0, -8";

        // How many tabs fit on the bottom row before running into the controls.
        const float gap = 12f;           // breathing room between last tab and controls
        float bottomArea = controlsLeft - railLeft - gap;
        int bottomCapacity = System.Math.Max(0, (int)((bottomArea + interMargin) / footprint));
        int bottomCount = System.Math.Min(Tabs.Count, bottomCapacity);
        for (int i = 0; i < bottomCount; i++)
            BottomRowTabs.Add(Tabs[i]);

        // Remaining tabs wrap to rows above the bottom row, using the full rail
        // width (frame width minus the rail's 36px left margin).
        float available = outerWidth - railLeft;
        int overflowCapacity = System.Math.Max(1, (int)((available + interMargin) / footprint));
        var chunks = new List<List<TabRow>>();
        for (int i = bottomCount; i < Tabs.Count; i += overflowCapacity)
        {
            var chunk = new List<TabRow>();
            for (int j = i; j < System.Math.Min(i + overflowCapacity, Tabs.Count); j++)
                chunk.Add(Tabs[j]);
            chunks.Add(chunk);
        }

        // Emit highest chunk first so the first overflow row sits directly above
        // the bottom row (the vertical lane renders top-to-bottom).
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            var group = new TabRowGroup();
            foreach (var t in chunks[i]) group.Tabs.Add(t);
            OverflowRowGroups.Add(group);
        }
    }

    // Cuts a label to fit maxWidth (in the given font), appending an ellipsis.
    private static string Truncate(string s, Microsoft.Xna.Framework.Graphics.SpriteFont font, float maxWidth)
    {
        if (string.IsNullOrEmpty(s)) return s;
        try
        {
            if (font.MeasureString(s).X <= maxWidth) return s;
            const string ellipsis = "...";
            float ellipsisW = font.MeasureString(ellipsis).X;
            int keep = 0;
            for (int i = 1; i <= s.Length; i++)
            {
                if (font.MeasureString(s.Substring(0, i)).X + ellipsisW > maxWidth) break;
                keep = i;
            }
            return s.Substring(0, keep).TrimEnd() + ellipsis;
        }
        catch { return s; }
    }

    private TabRow? FindTab(string id)
    {
        foreach (var t in Tabs)
            if (t.Id == id) return t;
        return null;
    }

    private void ReapplyFilter()
    {
        Quests.Clear();
        // Claimable quests ride at the top, unsorted, so a freshly finished
        // quest's reward is the first thing the player sees.
        var claimable = new List<QuestRow>();
        var collected = new List<QuestRow>();
        var activeTab = FindTab(_activeTabId);
        if (activeTab?.CustomDef is CustomTabDef def)
        {
            // Custom tabs search across all buckets and keep only the rows
            // matching the saved filters. A row only ever lives in one bucket,
            // so there are no duplicates.
            AddMatching(claimable, _claimableRows, def);
            AddMatching(claimable, _claimableOrderRows, def);
            AddMatching(collected, _activeRows, def);
            AddMatching(collected, _specialOrderRows, def);
            AddMatching(collected, _historyRows, def);
        }
        else switch (_activeTabId)
        {
            case TabActive:
                foreach (var r in _claimableRows) AddRow(claimable, r);
                foreach (var r in _activeRows) AddRow(collected, r);
                break;
            case TabSpecial:
                foreach (var r in _claimableOrderRows) AddRow(claimable, r);
                foreach (var r in _specialOrderRows) AddRow(collected, r);
                break;
            case TabCompleted:
                foreach (var r in _historyRows) AddRow(collected, r);
                break;
            case TabAll:
                foreach (var r in _claimableRows) AddRow(claimable, r);
                foreach (var r in _claimableOrderRows) AddRow(claimable, r);
                foreach (var r in _activeRows) AddRow(collected, r);
                foreach (var r in _specialOrderRows) AddRow(collected, r);
                foreach (var r in _historyRows) AddRow(collected, r);
                break;
        }

        foreach (var r in claimable)
            Quests.Add(r);
        foreach (var r in SortRows(collected))
            Quests.Add(r);

        // Rows are shared across tabs, so clear any stale selection/hover from
        // a previous tab before re-selecting. Divider goes under every row
        // except the last, so the list reads as separated entries without a
        // dangling line at the bottom.
        for (int i = 0; i < Quests.Count; i++)
        {
            Quests[i].IsSelected = false;
            Quests[i].ClearHover();
            Quests[i].ShowDivider = i < Quests.Count - 1;
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

    private void AddMatching(List<QuestRow> dest, List<QuestRow> source, CustomTabDef def)
    {
        foreach (var r in source)
            if (MatchesFilter(r, def)) AddRow(dest, r);
    }

    // Final gate before a row reaches the list: the search box. Empty search
    // lets everything through.
    private void AddRow(List<QuestRow> dest, QuestRow r)
    {
        if (Contains(r.Title, _searchText)) dest.Add(r);
    }

    // Orders the gathered rows by the current sort mode. OrderBy is stable, so
    // ties keep their natural (bucket) order. Deadline and Category push their
    // "empty" rows (no deadline / no category) to the bottom.
    private IEnumerable<QuestRow> SortRows(List<QuestRow> rows)
    {
        switch (_sortMode)
        {
            case SortMode.Alphabetical:
                return rows.OrderBy(r => r.Title, System.StringComparer.OrdinalIgnoreCase);
            case SortMode.Giver:
                return rows.OrderBy(r => r.GiverDisplay, System.StringComparer.OrdinalIgnoreCase)
                           .ThenBy(r => r.Title, System.StringComparer.OrdinalIgnoreCase);
            case SortMode.Source:
                return rows.OrderBy(r => r.SourceDisplay, System.StringComparer.OrdinalIgnoreCase)
                           .ThenBy(r => r.Title, System.StringComparer.OrdinalIgnoreCase);
            case SortMode.Category:
                return rows.OrderBy(r => string.IsNullOrWhiteSpace(r.Category) ? 1 : 0)
                           .ThenBy(r => r.Category, System.StringComparer.OrdinalIgnoreCase)
                           .ThenBy(r => r.Title, System.StringComparer.OrdinalIgnoreCase);
            case SortMode.Deadline:
            default:
                return rows.OrderBy(r => r.DeadlineDays.HasValue ? 0 : 1)
                           .ThenBy(r => r.DeadlineDays ?? int.MaxValue);
        }
    }

    private static bool MatchesFilter(QuestRow r, CustomTabDef def)
    {
        // Every non-blank filter must match (AND). Blank filters are ignored.
        // The Kind field ignores spaces so "Special Orders" matches "SpecialOrder"
        // and "Daily Board" matches "DailyBoard".
        return MatchesTextFilter(r.Title, def.TitleFilter)
            && MatchesTextFilter(r.SourceDisplay, def.SourceFilter)
            && MatchesTextFilter(r.Category, def.CategoryFilter)
            && MatchesTextFilter(r.Kind, def.KindFilter, ignoreSpaces: true)
            && MatchesDeadlineFilter(r, def.DeadlineFilter);
    }

    // A custom-tab text field, split on commas into terms. Plain terms are OR-ed
    // (the field must contain at least one of them), and a term starting with "!"
    // excludes (the field must not contain it). So "RSV, More Quests" keeps rows
    // from either, and "!Robin, crop order" keeps "crop order" rows that don't
    // mention Robin. A bare "!" is ignored. ignoreSpaces strips spaces from both
    // sides before comparing, so a kind typed with spaces still matches
    // ("Special Orders" finds "SpecialOrder").
    private static bool MatchesTextFilter(string? haystack, string? filter, bool ignoreSpaces = false)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        string hay = haystack ?? string.Empty;
        bool anyPositive = false, positiveHit = false;
        foreach (string raw in filter.Split(','))
        {
            string term = raw.Trim();
            if (term.Length == 0) continue;
            bool negate = term[0] == '!';
            if (negate)
            {
                term = term.Substring(1).Trim();
                if (term.Length == 0) continue;
            }
            bool contains = ContainsTerm(hay, term, ignoreSpaces);
            if (negate)
            {
                if (contains) return false;
            }
            else
            {
                anyPositive = true;
                if (contains) positiveHit = true;
            }
        }
        return !anyPositive || positiveHit;
    }

    private static bool ContainsTerm(string haystack, string needle, bool ignoreSpaces)
    {
        if (ignoreSpaces)
        {
            haystack = haystack.Replace(" ", string.Empty);
            needle = needle.Replace(" ", string.Empty);
        }
        return haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Deadline field, also comma-separated and OR/exclude like the text fields.
    // Each term can be: "None" (no deadline), a number N (exactly N days), a
    // range "A-B" (inclusive), or a comparison ">N" / ">=N" / "<N" / "<=N". So
    // "5" is exactly 5 days, "<=5" is 5 or fewer, "3, 7" is exactly 3 or 7, and
    // "None, >28" is "no deadline or more than 28 days out". A "!" term excludes,
    // so "<=3, !2" is "3 or fewer except exactly 2". Completed history has no
    // live deadline, so it's dropped whenever this field is set. Unparseable
    // terms are ignored.
    private static bool MatchesDeadlineFilter(QuestRow r, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        if (r.IsCompleted) return false;
        int? d = r.DeadlineDays;
        bool anyPositive = false, positiveHit = false;
        foreach (string raw in filter.Split(','))
        {
            string term = raw.Trim();
            if (term.Length == 0) continue;
            bool negate = term[0] == '!';
            if (negate)
            {
                term = term.Substring(1).Trim();
                if (term.Length == 0) continue;
            }
            if (!TryMatchDeadlineTerm(term, d, out bool matched))
                continue;
            if (negate)
            {
                if (matched) return false;
            }
            else
            {
                anyPositive = true;
                if (matched) positiveHit = true;
            }
        }
        return !anyPositive || positiveHit;
    }

    // Parses one deadline term and reports whether the value d satisfies it.
    // Returns false (term ignored) when it can't be parsed. A bare number means
    // exactly that many days; use "<=N" for "N or fewer".
    private static bool TryMatchDeadlineTerm(string term, int? d, out bool matched)
    {
        matched = false;
        string t = term.Replace(" ", string.Empty);
        if (t.Length == 0) return false;

        if (t.Equals("None", System.StringComparison.OrdinalIgnoreCase))
        {
            matched = !d.HasValue;
            return true;
        }

        // Range "A-B" inclusive. The dash is searched from index 1 so a leading
        // minus sign isn't mistaken for a separator.
        int dash = t.IndexOf('-', 1);
        if (dash > 0 && dash < t.Length - 1
            && int.TryParse(t.Substring(0, dash), out int lo)
            && int.TryParse(t.Substring(dash + 1), out int hi))
        {
            if (lo > hi) (lo, hi) = (hi, lo);
            matched = d.HasValue && d.Value >= lo && d.Value <= hi;
            return true;
        }

        if (t.StartsWith(">=")) return TryDeadlineCompare(t, 2, d, (v, x) => v >= x, out matched);
        if (t.StartsWith("<=")) return TryDeadlineCompare(t, 2, d, (v, x) => v <= x, out matched);
        if (t.StartsWith(">")) return TryDeadlineCompare(t, 1, d, (v, x) => v > x, out matched);
        if (t.StartsWith("<")) return TryDeadlineCompare(t, 1, d, (v, x) => v < x, out matched);

        if (int.TryParse(t, out int n))
        {
            matched = d.HasValue && d.Value == n;
            return true;
        }
        return false;
    }

    private static bool TryDeadlineCompare(string t, int skip, int? d, System.Func<int, int, bool> cmp, out bool matched)
    {
        matched = false;
        if (!int.TryParse(t.Substring(skip), out int x)) return false;
        matched = d.HasValue && cmp(d.Value, x);
        return true;
    }

    // Plain substring match used by the search box (no negation, no space trick).
    private static bool Contains(string? haystack, string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return haystack != null
            && haystack.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Collects the distinct category/kind values present across the player's
    // current quests so the editor popup can hint what's worth typing. Empty
    // when nothing in the journal is classified (e.g. vanilla-only save).
    private string BuildHint(System.Func<QuestRow, string> selector)
    {
        var seen = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
        void Scan(List<QuestRow> rows)
        {
            foreach (var r in rows)
            {
                string v = selector(r);
                if (!string.IsNullOrWhiteSpace(v)) seen.Add(v.Trim());
            }
        }
        Scan(_activeRows);
        Scan(_specialOrderRows);
        Scan(_historyRows);
        if (seen.Count == 0) return string.Empty;
        string values = string.Join(", ", seen);
        return _helper.Translation.Get("tabeditor.hint", new { values }).Default($"In your quests: {values}").ToString();
    }

    private QuestRow BuildActiveRow(Quest q)
    {
        var rewards = BuildRewardLines(q);
        var steps = BuildAdventureSteps(q);
        var (category, kind) = QuestSnapshotBuilder.ResolveCategoryKind(q, _mqfApi);
        return new QuestRow(
            title: q.questTitle ?? string.Empty,
            description: q.questDescription ?? string.Empty,
            objective: q.currentObjective ?? string.Empty,
            rewardLines: rewards,
            adventureSteps: steps,
            giverDisplay: ResolveGiverDisplay(q),
            daysLeftDisplay: BuildDaysLeftDisplay(q),
            sourceDisplay: ResolveSourceDisplay(q),
            warpTargets: BuildWarpTargets(q),
            isCompleted: false,
            canCancel: q.canBeCancelled.Value,
            canPostpone: q.daysLeft.Value > 0,
            quest: q,
            host: this,
            category: category,
            kind: kind,
            deadlineDays: DeadlineFromQuestCounter(q.daysLeft.Value));
    }

    // A completed quest still in the log, shown with a Claim button so the player
    // can take its reward. Mirrors BuildActiveRow but flagged completed/claimable:
    // no objective steps, deadline, cancel, or postpone. The reward lines still
    // come through so the detail panel shows what's waiting.
    private QuestRow BuildClaimableRow(Quest q)
    {
        var rewards = BuildRewardLines(q);
        var (category, kind) = QuestSnapshotBuilder.ResolveCategoryKind(q, _mqfApi);
        return new QuestRow(
            title: q.questTitle ?? string.Empty,
            description: q.questDescription ?? string.Empty,
            objective: string.Empty,
            rewardLines: rewards,
            adventureSteps: new List<AdventureStepRow>(),
            giverDisplay: ResolveGiverDisplay(q),
            daysLeftDisplay: _helper.Translation.Get("journal.claim.ready").Default("Reward ready to collect").ToString(),
            sourceDisplay: ResolveSourceDisplay(q),
            warpTargets: null,
            isCompleted: true,
            canCancel: false,
            canPostpone: false,
            quest: q,
            host: this,
            category: category,
            kind: kind,
            canClaim: true);
    }

    // A completed Special Order whose gold hasn't been collected yet, shown with
    // a Claim button. Only the money is outstanding here: vanilla auto-grants an
    // order's other rewards (gems, items, friendship) the moment it completes and
    // pays just the gold when you click its reward box, so that's all we surface.
    private QuestRow BuildClaimableOrderRow(SpecialOrder so)
    {
        int money = so.GetMoneyReward();
        var rewards = new List<RewardLineRow>
        {
            new RewardLineRow(kind: "Money", summary: T("journal.reward.money", new { amount = money }, $"{money}g"), amount: money)
        };
        return new QuestRow(
            title: ResolveSoTitle(so),
            description: SafeParse(so, so.questDescription.Value),
            objective: string.Empty,
            rewardLines: rewards,
            adventureSteps: new List<AdventureStepRow>(),
            giverDisplay: ResolveNpcDisplayName(so.requester.Value),
            daysLeftDisplay: _helper.Translation.Get("journal.claim.ready").Default("Reward ready to collect").ToString(),
            sourceDisplay: QuestSnapshotBuilder.ResolveSpecialOrderSource(so, _helper),
            warpTargets: null,
            isCompleted: true,
            canCancel: false,
            canPostpone: false,
            quest: null,
            host: this,
            category: string.Empty,
            kind: "SpecialOrder",
            specialOrder: so,
            canClaim: true);
    }

    // Turns a raw daysLeft counter into the "days until deadline" number we show:
    // a counter of 1 is the final day (0 days out), 2 is due tomorrow (1), and so
    // on. A counter of 0 or less means the quest has no time limit.
    private static int? DeadlineFromQuestCounter(int counter)
        => counter <= 0 ? (int?)null : counter - 1;

    private QuestRow BuildSpecialOrderRow(SpecialOrder so)
    {
        // SOs use OrderObjective lists; render them through the existing
        // AdventureStepRow surface so the SML view doesn't have to learn a
        // third row type. Active highlighting is off because SO objectives
        // can complete in any order (no Requires graph).
        var steps = new List<AdventureStepRow>();
        int idx = 0;
        foreach (OrderObjective obj in so.objectives)
        {
            if (obj == null) continue;
            string desc = SafeParse(so, obj.GetDescription());
            steps.Add(new AdventureStepRow(
                index: idx++,
                description: desc,
                progress: obj.GetCount(),
                count: obj.GetMaxCount(),
                done: obj.IsComplete(),
                active: false,
                kind: "Objective"));
        }

        var rewards = BuildSpecialOrderRewards(so);
        if (rewards.Count == 0)
            rewards.Add(new RewardLineRow(kind: "None", summary: "(none)"));

        string source = QuestSnapshotBuilder.ResolveSpecialOrderSource(so, _helper);
        string giver = ResolveNpcDisplayName(so.requester.Value);

        // Special orders always carry a due date, so they never read as "no
        // deadline". dueDate is absolute, so subtract today to get the counter,
        // then clamp the final-day case to 0 instead of letting it go negative.
        int soDaysLeft = so.dueDate.Value - (Game1.Date?.TotalDays ?? 0);
        int soDeadline = System.Math.Max(0, soDaysLeft - 1);

        return new QuestRow(
            title: ResolveSoTitle(so),
            description: SafeParse(so, so.questDescription.Value),
            objective: string.Empty,
            rewardLines: rewards,
            adventureSteps: steps,
            giverDisplay: giver,
            daysLeftDisplay: BuildSpecialOrderDaysLeft(so),
            sourceDisplay: source,
            warpTargets: BuildSoWarpTargets(so),
            isCompleted: false,
            canCancel: false,
            canPostpone: false,
            quest: null,
            specialOrder: so,
            host: this,
            category: string.Empty,
            kind: "SpecialOrder",
            deadlineDays: soDeadline);
    }

    private static List<RewardLineRow> BuildSpecialOrderRewards(SpecialOrder so)
    {
        // SO rewards are an OrderReward polymorphic list rather than a single
        // money payout. Vanilla ships five concrete subclasses; we render the
        // four that are player-facing (Money / Gems / Friendship / Object)
        // and skip Mail and ResetEvent since they're internal mechanics with
        // no useful surface for the journal. Each branch is wrapped so a
        // modded subclass throwing on a getter can't take the whole journal
        // down; on failure we add a placeholder line so the player sees
        // there's something there.
        var lines = new List<RewardLineRow>();
        foreach (OrderReward reward in so.rewards)
        {
            if (reward == null) continue;
            try
            {
            switch (reward)
            {
                case MoneyReward mr:
                {
                    int amt = mr.GetRewardMoneyAmount();
                    if (amt > 0)
                        lines.Add(new RewardLineRow(kind: "Money", summary: T("journal.reward.money", new { amount = amt }, $"{amt}g"), amount: amt));
                    break;
                }
                case GemsReward gr:
                {
                    int amt = gr.amount.Value;
                    if (amt > 0)
                        lines.Add(new RewardLineRow(kind: "Gems", summary: T("journal.reward.qigems", new { amount = amt }, $"{amt} Qi Gems"), amount: amt));
                    break;
                }
                case FriendshipReward fr:
                {
                    string target = ResolveNpcDisplayName(fr.targetName.Value);
                    int amt = fr.amount.Value;
                    if (amt != 0)
                        lines.Add(new RewardLineRow(
                            kind: "Friendship",
                            summary: T("journal.reward.friendship", new { amount = amt, npc = target }, $"+{amt} friendship with {target}"),
                            npcName: fr.targetName.Value,
                            amount: amt));
                    break;
                }
                case ObjectReward objRew:
                {
                    string itemKey = objRew.itemKey.Value ?? string.Empty;
                    int amt = objRew.amount.Value;
                    if (string.IsNullOrEmpty(itemKey) || amt <= 0) break;
                    string itemName = ResolveItemDisplayName(itemKey);
                    string summary = amt > 1 ? T("journal.reward.item", new { amount = amt, item = itemName }, $"{amt} {itemName}") : itemName;
                    lines.Add(new RewardLineRow(
                        kind: "Item",
                        summary: summary,
                        itemId: itemKey,
                        amount: amt));
                    break;
                }
            }
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor?.Log(
                    $"Failed to render SO reward of type {reward.GetType().Name} for quest '{so.questKey.Value}': {ex.Message}",
                    StardewModdingAPI.LogLevel.Warn);
                lines.Add(new RewardLineRow(kind: "Other", summary: T("journal.reward.extra", "(extra reward)")));
            }
        }
        return lines;
    }

    private static string ResolveNpcDisplayName(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName)) return T("journal.unknown", "Unknown");
        try
        {
            var npc = Game1.getCharacterFromName(internalName);
            if (npc != null && !string.IsNullOrEmpty(npc.displayName))
                return AsciiFold(npc.displayName);
        }
        catch { }
        return AsciiFold(internalName!);
    }

    // StardewUI's text rendering hangs the game when it encounters glyphs
    // missing from the SpriteFont atlas (seen with the modded NPC name
    // "Adelaide" written as "Adélaïde"). Stripping diacritics via
    // Unicode FormD decomposition gives us plain ASCII fallbacks the font
    // is guaranteed to have. Applied only to strings we generate or
    // surface in our own rendered content; vanilla strings (descriptions,
    // parsed objectives) pass through untouched since vanilla renders them
    // fine on its own.
    private static string AsciiFold(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        bool anyHighChar = false;
        for (int i = 0; i < s.Length; i++) { if (s[i] > 127) { anyHighChar = true; break; } }
        if (!anyHighChar) return s;
        string decomposed = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static string ResolveItemDisplayName(string itemKey)
    {
        try
        {
            ParsedItemData? data = ItemRegistry.GetData(itemKey);
            if (data != null && !string.IsNullOrEmpty(data.DisplayName))
                return data.DisplayName;
        }
        catch { }
        return itemKey;
    }

    private static string SafeParse(SpecialOrder so, string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        try { return so.Parse(raw) ?? raw!; }
        catch { return raw!; }
    }

    // SpecialOrder.GetName() caches its result in _localizedName the first time
    // it's called. If that first call happens before a content pack (like
    // Ridgeside Village) has patched its [key] strings into
    // Strings/SpecialOrderStrings, the unresolved fallback path gets cached and
    // every later read shows the literal "Strings\SpecialOrderStrings:..." key.
    // The objective rows already dodge this by going through the cache-free
    // Parse(), so we resolve the title fresh from the raw field the same way.
    private static string ResolveSoTitle(SpecialOrder so)
    {
        string? raw = so.questName.Value;
        if (string.IsNullOrEmpty(raw)) return so.GetName() ?? string.Empty;
        try { return SpecialOrder.MakeLocalizationReplacements(raw).Trim(); }
        catch { return so.GetName() ?? string.Empty; }
    }

    private string BuildSpecialOrderDaysLeft(SpecialOrder so)
    {
        // SpecialOrder.dueDate.Value is absolute TotalDays (when the order
        // expires), not a countdown. Subtract Game1.Date.TotalDays to get the same
        // counter quests use, then run it through the shared deadline wording. The
        // game fails an order the morning its counter hits 0, just like quests, so
        // the two read the same in the journal.
        int totalDays = Game1.Date?.TotalDays ?? 0;
        int daysLeft = so.dueDate.Value - totalDays;
        return BuildDeadlineDisplay(daysLeft);
    }

    private QuestRow BuildHistoryRow(CompletedQuestRecord r)
    {
        bool failed = string.Equals(r.Status, "Failed", System.StringComparison.OrdinalIgnoreCase);
        string statusLabel = failed
            ? _helper.Translation.Get("journal.status.failed").Default("Failed").ToString()
            : _helper.Translation.Get("journal.status.completed").Default("Completed").ToString();
        // CompletedOnTotalDays==0 happens on pre-fix records where we never
        // wrote the field. Fall back to the bare status string so old rows
        // still read sanely instead of saying "Completed Spring 1, Y1".
        string dateSlot = r.CompletedOnTotalDays > 0
            ? $"{statusLabel} {BuildHistoryDateDisplay(r.CompletedOnTotalDays)}"
            : statusLabel;
        var rewards = new List<RewardLineRow>();
        if (r.RewardLines != null && r.RewardLines.Count > 0)
        {
            foreach (var l in r.RewardLines)
            {
                rewards.Add(new RewardLineRow(
                    kind: l.Kind ?? string.Empty,
                    summary: l.Summary ?? string.Empty,
                    itemId: l.ItemId,
                    npcName: l.NpcName,
                    amount: l.Amount,
                    durationDays: l.DurationDays));
            }
        }
        else if (!string.IsNullOrEmpty(r.RewardSummary))
        {
            // Older history records (pre Step 5) only stored an aggregate
            // string. Surface it as a single "Custom" line so the panel still
            // renders something readable.
            rewards.Add(new RewardLineRow(kind: "Custom", summary: r.RewardSummary));
        }
        else
        {
            rewards.Add(new RewardLineRow(kind: "None", summary: "(none)"));
        }

        return new QuestRow(
            title: r.Title,
            description: r.Description,
            objective: r.Objective,
            rewardLines: rewards,
            adventureSteps: new List<AdventureStepRow>(),
            giverDisplay: string.IsNullOrEmpty(r.Giver) ? UnknownLabel : r.Giver,
            daysLeftDisplay: dateSlot,
            sourceDisplay: string.IsNullOrEmpty(r.Source) ? UnknownLabel : r.Source,
            warpTargets: null,
            isCompleted: true,
            canCancel: false,
            canPostpone: false,
            quest: null,
            host: this,
            category: r.Category ?? string.Empty,
            kind: r.Kind ?? string.Empty);
    }

    private static CompletedQuestRecord BuildRecordFrom(QuestRow row)
    {
        var stored = new List<StoredRewardLine>();
        foreach (var l in row.RewardLines)
        {
            stored.Add(new StoredRewardLine
            {
                Kind = l.Kind,
                Summary = l.Summary,
                ItemId = l.ItemId,
                NpcName = l.NpcName,
                Amount = l.Amount,
                DurationDays = l.DurationDays
            });
        }
        return new CompletedQuestRecord
        {
            Title = row.Title,
            Description = row.Description,
            Objective = row.Objective,
            RewardSummary = row.RewardSummaryAggregate,
            RewardLines = stored,
            Giver = row.GiverDisplay,
            Source = row.SourceDisplay,
            Category = row.Category,
            Kind = row.Kind,
            CompletedOnTotalDays = Game1.Date?.TotalDays ?? 0
        };
    }

    private void UpdateTabSelection()
    {
        foreach (var t in Tabs)
            t.IsActive = (t.Id == _activeTabId);
    }

    private List<RewardLineRow> BuildRewardLines(Quest q)
        => QuestSnapshotBuilder.BuildRewardLines(q, _mqfApi);

    private IReadOnlyList<IAdventureStepInfo>? SafeGetAdventureSteps(Quest q)
    {
        try { return _mqfApi!.GetAdventureSteps(q); }
        catch { return null; }
    }

    private int? SafeGetActiveStepIndex(Quest q)
    {
        try { return _mqfApi!.GetActiveStepIndex(q); }
        catch { return null; }
    }

    private List<AdventureStepRow> BuildAdventureSteps(Quest q)
    {
        var rows = new List<AdventureStepRow>();
        if (_mqfApi == null) return rows;

        var steps = SafeGetAdventureSteps(q);
        if (steps == null || steps.Count == 0) return rows;

        // GetActiveStepIndex flags the row to highlight. Null means every
        // step is done; we'll fall back to whichever step still reports
        // Active so a finished-but-not-cleared quest still renders sanely.
        int? activeIdx = SafeGetActiveStepIndex(q);
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            bool active = activeIdx.HasValue ? i == activeIdx.Value : s.Active;
            rows.Add(new AdventureStepRow(
                index: i,
                description: s.Description,
                progress: s.Progress,
                count: s.Count,
                done: s.Done,
                active: active && !s.Done,
                kind: s.Kind));
        }
        return rows;
    }

    // The NPCs a quest can warp to: its giver plus any NPC named in the active
    // Adventure step's Targets. Each is filtered to a character that actually
    // resolves so the dropdown never offers a warp that goes nowhere.
    private List<WarpNpc> BuildWarpTargets(Quest q)
    {
        var result = new List<WarpNpc>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        void Add(string? internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return;
            string name = internalName!.Trim();
            if (name.Length == 0 || !seen.Add(name)) return;
            if (Game1.getCharacterFromName(name) == null) return;
            result.Add(new WarpNpc(name, ResolveNpcDisplayName(name)));
        }

        Add(ResolveGiverNpcName(q));

        var steps = SafeGetAdventureSteps(q);
        if (steps != null && steps.Count > 0)
        {
            int? activeIdx = SafeGetActiveStepIndex(q);
            if (activeIdx.HasValue && activeIdx.Value >= 0 && activeIdx.Value < steps.Count)
            {
                var targets = steps[activeIdx.Value].Targets;
                if (targets != null)
                    foreach (var t in targets) Add(t);
            }
        }

        return result;
    }

    private static List<WarpNpc> BuildSoWarpTargets(SpecialOrder so)
    {
        var result = new List<WarpNpc>();
        string? req = so.requester.Value;
        if (!string.IsNullOrEmpty(req) && Game1.getCharacterFromName(req) != null)
            result.Add(new WarpNpc(req!, ResolveNpcDisplayName(req)));
        return result;
    }

    private void RebuildSelectedRewards()
    {
        SelectedRewards.Clear();
        if (_selectedQuest == null) return;
        foreach (var line in _selectedQuest.RewardLines)
            SelectedRewards.Add(line);
    }

    private void RebuildSelectedSteps()
    {
        SelectedSteps.Clear();
        if (_selectedQuest == null) return;
        foreach (var s in _selectedQuest.AdventureSteps)
            SelectedSteps.Add(s);
    }

    private string BuildDaysLeftDisplay(Quest q)
    {
        int d = q.daysLeft.Value;
        if (d <= 0) return _helper.Translation.Get("journal.days.none").Default("No deadline").ToString();
        return BuildDeadlineDisplay(d);
    }

    // Turns the game's raw day counter into a friendly, accurate deadline label.
    // For quests this is daysLeft; for special orders it's dueDate minus today.
    // Either way the quest/order is pulled the morning the counter would hit 0, so
    // a counter of 1 means today is the last day to finish it. The real deadline is
    // (counter - 1) days out: 1 is today (Final day), 2 is tomorrow (Due tomorrow),
    // and anything higher counts down the days until the deadline. The base game's
    // own quest screen says "Final Day" for the last day, so reuse its string so we
    // match that wording in every language.
    private string BuildDeadlineDisplay(int counter)
    {
        int untilDeadline = counter - 1;
        if (untilDeadline <= 0)
        {
            try { return Game1.content.LoadString("Strings\\StringsFromCSFiles:Quest_FinalDay"); }
            catch { return _helper.Translation.Get("journal.days.finalday").Default("Final day!").ToString(); }
        }
        if (untilDeadline == 1)
            return _helper.Translation.Get("journal.days.duetomorrow").Default("Due tomorrow!").ToString();
        return _helper.Translation.Get("journal.days.left", new { count = untilDeadline }).Default($"{untilDeadline} days left").ToString();
    }

    private static string BuildHistoryDateDisplay(int totalDays)
    {
        // Inverse of WorldDate.TotalDays: (year-1)*112 + season*28 + (day-1).
        // 4 seasons of 28 days each. TotalDays==0 == Spring 1, Y1.
        if (totalDays < 0) totalDays = 0;
        int year = totalDays / 112 + 1;
        int remainder = totalDays % 112;
        int seasonIdx = remainder / 28;
        int day = remainder % 28 + 1;
        string season = seasonIdx switch
        {
            0 => "Spring",
            1 => "Summer",
            2 => "Fall",
            _ => "Winter"
        };
        return $"{season} {day}, Y{year}";
    }

    private string ResolveGiverDisplay(Quest q) => QuestSnapshotBuilder.ResolveGiverDisplay(q, _mqfApi);
    private string? ResolveGiverNpcName(Quest q) => QuestSnapshotBuilder.ResolveGiverNpcName(q, _mqfApi);
    private string ResolveSourceDisplay(Quest q) => QuestSnapshotBuilder.ResolveSourceDisplay(q, _mqfApi, _helper);

    private void RaiseSelectionDependents()
    {
        Raise(nameof(SelectedTitle));
        Raise(nameof(SelectedDescription));
        Raise(nameof(SelectedObjective));
        Raise(nameof(SelectedGiverDisplay));
        Raise(nameof(SelectedDaysLeftDisplay));
        Raise(nameof(SelectedSourceDisplay));
        Raise(nameof(SelectedWarpLabel));
        Raise(nameof(SelectedIsCompleted));
        Raise(nameof(SelectedShowActions));
        Raise(nameof(SelectedShowComplete));
        Raise(nameof(SelectedCanClaim));
        Raise(nameof(SelectedShowCancel));
        Raise(nameof(SelectedShowPostpone));
        Raise(nameof(SelectedShowDetails));
        Raise(nameof(SelectedShowPin));
        Raise(nameof(SelectedShowWarp));
        Raise(nameof(SelectedShowItemHelper));
        Raise(nameof(SelectedItemHelperLabel));
        Raise(nameof(SelectedIsPinned));
        Raise(nameof(SelectedPinLabel));
        Raise(nameof(SelectedHasSteps));
        Raise(nameof(SelectedShowObjective));
        Raise(nameof(HasSelection));
        Raise(nameof(NoSelection));
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class TabRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    // Settable so the "Edit tabs"/"Done" control tab can flip its own label.
    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set { if (_label == value) return; _label = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label))); }
    }
    // Non-null only for player-defined tabs. In edit mode, clicking a custom
    // tab opens its editor instead of switching to it.
    public CustomTabDef? CustomDef { get; }
    public bool IsCustom => CustomDef != null;

    // The two rail controls render an icon instead of a label and use a narrow
    // icon-sized width (the rest of the rail stays uniform). Everything else is
    // a normal text tab.
    public bool IsAddTab { get; init; }
    public bool IsEditTab { get; init; }
    public bool IsTextTab => !IsAddTab && !IsEditTab;
    private readonly System.Action<TabRow> _onActivate;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TabOpacity)));
        }
    }

    // Inactive tabs dim slightly so the active one reads as selected (the tab
    // frames don't raise like vanilla tabs, so opacity carries the cue).
    public float TabOpacity => _isActive ? 1f : 0.85f;

    // Set by RebuildTabRows so every tab is the same width (uniform rail). The
    // full name lives in Label (used for the tooltip); DisplayLabel is the text
    // actually drawn, truncated to fit the uniform width.
    private string _displayLabel = string.Empty;
    public string DisplayLabel
    {
        get => _displayLabel;
        set { if (_displayLabel == value) return; _displayLabel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayLabel))); }
    }

    private string _widthLayout = "content content";
    public string WidthLayout
    {
        get => _widthLayout;
        set { if (_widthLayout == value) return; _widthLayout = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WidthLayout))); }
    }

    public TabRow(string id, string label, System.Action<TabRow> onActivate, CustomTabDef? customDef = null)
    {
        Id = id;
        Label = label;
        _onActivate = onActivate;
        CustomDef = customDef;
    }

    public void Activate() => _onActivate(this);
}

// One horizontal row of tabs on the rail. RebuildTabRows fills these so the
// SML can repeat rows (vertical) then tabs within each row (horizontal).
public sealed class TabRowGroup
{
    public ObservableCollection<TabRow> Tabs { get; } = new();
}

public sealed class QuestRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }
    public string Description { get; }
    public string Objective { get; }
    public IReadOnlyList<RewardLineRow> RewardLines { get; }
    public IReadOnlyList<AdventureStepRow> AdventureSteps { get; }
    public string GiverDisplay { get; }
    public string DaysLeftDisplay { get; }
    // Numeric days until the deadline, matching the number shown in DaysLeftDisplay
    // ("5 days left" == 5, "Final day" == 0). Null means no deadline at all, and
    // also for completed/history rows. Sorting and the deadline filter read this.
    public int? DeadlineDays { get; }
    public string SourceDisplay { get; }
    // MoreQuests category / posting kind as plain strings (e.g. "Cooking",
    // "DailyBoard"), used only by custom-tab filtering. Blank for vanilla quests
    // and for buckets we don't classify. Not shown in the detail panel.
    public string Category { get; }
    public string Kind { get; }
    // NPCs this quest can warp to (giver + active-step targets), distinct and
    // already filtered to characters that actually resolve. Empty for history
    // rows. One entry warps directly; more open the dropdown.
    public IReadOnlyList<WarpNpc> WarpTargets { get; }
    public bool IsCompleted { get; }
    public bool CanCancel { get; }
    public bool CanPostpone { get; }
    // True for a finished quest whose reward is still waiting to be collected.
    // Drives the Claim button and the list badge. Constant for a row's lifetime.
    public bool CanClaim { get; }
    public bool ReadyToClaim => CanClaim;
    // Live quest reference for active rows. Null for historical rows
    // (snapshots from CompletedQuestStore) and for special-order rows.
    public Quest? Quest { get; }
    // Live special-order reference for Special Orders tab rows. Null otherwise.
    public SpecialOrder? SpecialOrder { get; }
    public bool IsSpecialOrder => SpecialOrder != null;

    private readonly JournalContext _host;

    // Selection (sticks until another row is clicked) and hover both feed
    // RowTint. RowTint is the highlight colour drawn over the row's solid
    // background sprite: selected wins over hover, otherwise transparent.
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Raise(nameof(IsSelected));
            Raise(nameof(RowTint));
        }
    }

    private bool _isHovered;
    public bool IsHovered => _isHovered;

    private bool _showDivider;
    public bool ShowDivider
    {
        get => _showDivider;
        set { if (_showDivider == value) return; _showDivider = value; Raise(nameof(ShowDivider)); }
    }

    // Colours come from JournalTheme (a Content Patcher-editable asset), so a
    // re-theme patch flows through here. Selected wins over hover, else nothing.
    public Color RowTint => _isSelected ? JournalTheme.SelectedTint : (_isHovered ? JournalTheme.HoverTint : Color.Transparent);
    public Color DividerTint => JournalTheme.DividerColor;

    public void HoverEnter()
    {
        if (_isHovered) return;
        _isHovered = true;
        Raise(nameof(IsHovered));
        Raise(nameof(RowTint));
    }

    public void HoverLeave() => ClearHover();

    public void ClearHover()
    {
        if (!_isHovered) return;
        _isHovered = false;
        Raise(nameof(IsHovered));
        Raise(nameof(RowTint));
    }

    // Called when the theme asset is repatched so an open journal repaints.
    public void RaiseThemeColors()
    {
        Raise(nameof(RowTint));
        Raise(nameof(DividerTint));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Aggregate text capture for legacy CompletedQuestRecord.RewardSummary so
    // pre-Step-5 saves still render readable history rows after this change.
    public string RewardSummaryAggregate
    {
        get
        {
            if (RewardLines.Count == 0) return string.Empty;
            var parts = new List<string>(RewardLines.Count);
            foreach (var r in RewardLines)
            {
                if (!string.IsNullOrEmpty(r.Summary))
                    parts.Add(r.Summary);
            }
            return string.Join(", ", parts);
        }
    }

    public QuestRow(
        string title,
        string description,
        string objective,
        IReadOnlyList<RewardLineRow> rewardLines,
        IReadOnlyList<AdventureStepRow> adventureSteps,
        string giverDisplay,
        string daysLeftDisplay,
        string sourceDisplay,
        IReadOnlyList<WarpNpc>? warpTargets,
        bool isCompleted,
        bool canCancel,
        bool canPostpone,
        Quest? quest,
        JournalContext host,
        string category = "",
        string kind = "",
        SpecialOrder? specialOrder = null,
        int? deadlineDays = null,
        bool canClaim = false)
    {
        Title = title;
        Description = description;
        Objective = objective;
        RewardLines = rewardLines ?? new List<RewardLineRow>();
        AdventureSteps = adventureSteps ?? new List<AdventureStepRow>();
        GiverDisplay = giverDisplay;
        DaysLeftDisplay = daysLeftDisplay;
        DeadlineDays = deadlineDays;
        SourceDisplay = sourceDisplay;
        Category = category ?? string.Empty;
        Kind = kind ?? string.Empty;
        WarpTargets = warpTargets ?? new List<WarpNpc>();
        IsCompleted = isCompleted;
        CanCancel = canCancel;
        CanPostpone = canPostpone;
        CanClaim = canClaim;
        Quest = quest;
        SpecialOrder = specialOrder;
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
