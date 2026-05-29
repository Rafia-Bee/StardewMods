using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using QuestJournal.Api;
using QuestJournal.Integrations;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;
using StardewValley.ItemTypeDefinitions;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;

namespace QuestJournal.Menu;

public sealed class JournalContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TabRow> Tabs { get; } = new();
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
    public string SelectedWarpLabel => _selectedQuest?.WarpLabel ?? string.Empty;
    public bool SelectedIsCompleted => _selectedQuest?.IsCompleted == true;
    public bool SelectedShowActions => _selectedQuest != null && !_selectedQuest.IsCompleted && _selectedQuest.Quest != null;
    public bool SelectedShowComplete => SelectedShowActions;
    public bool SelectedShowCancel => _selectedQuest != null && _selectedQuest.CanCancel;
    public bool SelectedShowPostpone => _selectedQuest != null && _selectedQuest.CanPostpone;
    // SML swaps between the single Objective line and the multi-step list
    // based on these. A stepped Adventure quest renders the step list and
    // hides the objective; everything else keeps the single objective.
    public bool SelectedHasSteps => _selectedQuest?.AdventureSteps.Count > 0;
    public bool SelectedShowObjective => !SelectedHasSteps && !string.IsNullOrEmpty(SelectedObjective);

    public bool HasSelection => _selectedQuest != null;
    public bool IsEmpty => Quests.Count == 0;

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

    public string RootLayout => $"{Px(1100)} {Px(720)}";
    public string TabLayout => $"{Px(140)} {Px(76)}";
    public string PanelRowLayout => $"content {Px(580)}";
    public string ListPanelLayout => $"{Px(240)} {Px(580)}";
    public string DetailPanelLayout => $"{Px(484)} {Px(580)}";
    public string ActionPanelLayout => $"{Px(236)} {Px(580)}";
    public string ActionLaneLayout => $"stretch {Px(540)}";

    // Repaint an open journal after the theme asset is repatched.
    public void RefreshTheme()
    {
        Raise(nameof(HeaderColor));
        foreach (var r in Quests)
            r.RaiseThemeColors();
    }

    private string _activeTabId = TabActive;
    private List<QuestRow> _activeRows = new();
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
        Tabs.Add(new TabRow(TabActive, "Active", id => SelectTab(id)));
        Tabs.Add(new TabRow(TabSpecial, "Special Orders", id => SelectTab(id)));
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

        _specialOrderRows.Clear();
        // Special Orders live in player.team.specialOrders, not questLog, so
        // they need a parallel pass. Only in-progress orders go in the tab;
        // completed/failed ones are removed by vanilla within a day or two
        // and don't need a separate history bucket (yet).
        var orders = Game1.player?.team?.specialOrders;
        if (orders != null)
        {
            foreach (var so in orders)
            {
                if (so == null) continue;
                if (so.questState.Value != SpecialOrderStatus.InProgress) continue;
                _specialOrderRows.Add(BuildSpecialOrderRow(so));
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
        _activeTabId = id;
        UpdateTabSelection();
        ReapplyFilter();
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
                // The watcher would otherwise see the disappearance and
                // record this as Failed. The player chose to drop it,
                // so mark it ignored before the actual removal.
                _completionWatcher?.MarkIgnore(quest);
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
        var popupController = _viewEngine.CreateMenuControllerFromAsset($"{_viewPrefix}/quest_details", detailsCtx);
        if (popupController != null)
        {
            popupController.DimmingAmount = 0f;
            popupController.Closed += () => popupController.Dispose();
            Game1.activeClickableMenu?.SetChildMenu(popupController.Menu);
        }
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
            case TabSpecial:
                foreach (var r in _specialOrderRows) Quests.Add(r);
                break;
            case TabCompleted:
                foreach (var r in _historyRows) Quests.Add(r);
                break;
            case TabAll:
                foreach (var r in _activeRows) Quests.Add(r);
                foreach (var r in _specialOrderRows) Quests.Add(r);
                foreach (var r in _historyRows) Quests.Add(r);
                break;
        }
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

    private QuestRow BuildActiveRow(Quest q)
    {
        var rewards = BuildRewardLines(q);
        var steps = BuildAdventureSteps(q);
        return new QuestRow(
            title: q.questTitle ?? string.Empty,
            description: q.questDescription ?? string.Empty,
            objective: q.currentObjective ?? string.Empty,
            rewardLines: rewards,
            adventureSteps: steps,
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

        return new QuestRow(
            title: ResolveSoTitle(so),
            description: SafeParse(so, so.questDescription.Value),
            objective: string.Empty,
            rewardLines: rewards,
            adventureSteps: steps,
            giverDisplay: giver,
            daysLeftDisplay: BuildSpecialOrderDaysLeft(so),
            sourceDisplay: source,
            warpTarget: null,
            isCompleted: false,
            canCancel: false,
            canPostpone: false,
            quest: null,
            host: this);
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
                        lines.Add(new RewardLineRow(kind: "Money", summary: $"{amt}g", amount: amt));
                    break;
                }
                case GemsReward gr:
                {
                    int amt = gr.amount.Value;
                    if (amt > 0)
                        lines.Add(new RewardLineRow(kind: "Gems", summary: $"{amt} Qi Gems", amount: amt));
                    break;
                }
                case FriendshipReward fr:
                {
                    string target = ResolveNpcDisplayName(fr.targetName.Value);
                    int amt = fr.amount.Value;
                    if (amt != 0)
                        lines.Add(new RewardLineRow(
                            kind: "Friendship",
                            summary: $"+{amt} friendship with {target}",
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
                    string summary = amt > 1 ? $"{amt} {itemName}" : itemName;
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
                lines.Add(new RewardLineRow(kind: "Other", summary: "(extra reward)"));
            }
        }
        return lines;
    }

    private static string ResolveNpcDisplayName(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName)) return "Unknown";
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

    private static string BuildSpecialOrderDaysLeft(SpecialOrder so)
    {
        // SpecialOrder.dueDate.Value is absolute TotalDays (when the order
        // expires), not a countdown. Subtract Game1.Date.TotalDays to get
        // remaining days.
        int totalDays = Game1.Date?.TotalDays ?? 0;
        int daysLeft = so.dueDate.Value - totalDays;
        if (daysLeft <= 0) return "Due today!";
        if (daysLeft == 1) return "Due tomorrow!";
        return $"{daysLeft} days left";
    }

    private QuestRow BuildHistoryRow(CompletedQuestRecord r)
    {
        bool failed = string.Equals(r.Status, "Failed", System.StringComparison.OrdinalIgnoreCase);
        string statusLabel = failed ? "Failed" : "Completed";
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
            giverDisplay: string.IsNullOrEmpty(r.Giver) ? "Unknown" : r.Giver,
            daysLeftDisplay: dateSlot,
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

    private static string BuildDaysLeftDisplay(Quest q)
    {
        int d = q.daysLeft.Value;
        if (d <= 0) return "No deadline";
        // Vanilla yanks the quest the first morning daysLeft hits 0, so
        // daysLeft==1 means "you sleep tonight and it expires", not "you
        // have a full day". Flag it so the player notices before sleeping.
        if (d == 1) return "Due tomorrow!";
        return $"{d} days left";
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
        Raise(nameof(SelectedShowCancel));
        Raise(nameof(SelectedShowPostpone));
        Raise(nameof(SelectedHasSteps));
        Raise(nameof(SelectedShowObjective));
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
    public IReadOnlyList<RewardLineRow> RewardLines { get; }
    public IReadOnlyList<AdventureStepRow> AdventureSteps { get; }
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

    public bool HasWarpTarget => !string.IsNullOrEmpty(WarpTarget);
    public string WarpLabel => HasWarpTarget ? $"Warp to {WarpTarget}" : "No warp target";

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
        RewardLines = rewardLines ?? new List<RewardLineRow>();
        AdventureSteps = adventureSteps ?? new List<AdventureStepRow>();
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
