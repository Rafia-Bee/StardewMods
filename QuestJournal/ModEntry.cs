using QuestJournal.Api;
using QuestJournal.Hud;
using QuestJournal.Integrations;
using QuestJournal.Menu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace QuestJournal;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; private set; } = new();

    private IViewEngine? _viewEngine;
    private IMoreQuestsApi? _mqfApi;
    private string _viewPrefix = null!;
    private GameMenuTabOverlay? _tabOverlay;
    private CompletionWatcher? _completionWatcher;
    private PinnedObjectiveHud? _pinnedHud;
    private NewQuestPinner? _newQuestPinner;
    private IClickableMenu? _journalMenu;
    private JournalContext? _journalContext;

    // Journal drag state (handled via SMAPI input rather than StardewUI drag
    // events, which don't reliably fire on a plain frame). You can grab the
    // window anywhere; a press only becomes a drag once the cursor travels past
    // a small threshold, so plain clicks still reach tabs / buttons / rows.
    private bool _journalDragging;
    private bool _journalPendingDrag;
    private Microsoft.Xna.Framework.Vector2 _journalGrab;
    private Microsoft.Xna.Framework.Vector2 _journalPressPos;
    private const int JournalDragThreshold = 8;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();
        _viewPrefix = $"Mods/{ModManifest.UniqueID}/Views";

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Input.ButtonPressed += OnInputButtonPressed;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
    }

    // The journal's frame rectangle in UI-screen space when it's the active menu
    // with no child popup; null otherwise. The floating tab rail sits above this
    // rect, so tabs and the +/Edit controls are naturally excluded from dragging.
    private Microsoft.Xna.Framework.Rectangle? JournalFrameRect()
    {
        if (_journalContext == null || _journalMenu == null) return null;
        if (!ReferenceEquals(Game1.activeClickableMenu, _journalMenu)) return null;
        if (_journalMenu.GetChildMenu() != null) return null;
        var tl = _journalContext.GetJournalTopLeft();
        return new Microsoft.Xna.Framework.Rectangle(
            tl.X, tl.Y, _journalContext.JournalFrameWidth, _journalContext.JournalFrameHeight);
    }

    private void OnInputButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft) return;
        var frame = JournalFrameRect();
        if (frame == null) return;
        var cursor = e.Cursor.GetScaledScreenPixels();
        if (!frame.Value.Contains((int)cursor.X, (int)cursor.Y)) return;
        // Arm a potential drag without suppressing, so a plain click still
        // reaches the widget under it; the per-tick poll promotes it to a drag.
        var tl = _journalContext!.GetJournalTopLeft();
        _journalPendingDrag = true;
        _journalPressPos = cursor;
        _journalGrab = new Microsoft.Xna.Framework.Vector2(cursor.X - tl.X, cursor.Y - tl.Y);
    }

    // Per-tick drag driver. CursorMoved doesn't fire with a button held, and
    // SMAPI's ButtonReleased fires spuriously the tick after Suppress(), so the
    // whole lifecycle is polled here off the raw mouse state. A pending press
    // becomes a real drag only after the cursor moves past the threshold; until
    // then nothing is suppressed, so a plain click reaches the widget under it.
    private void UpdateJournalDrag()
    {
        if (!_journalPendingDrag && !_journalDragging) return;

        bool held = Microsoft.Xna.Framework.Input.Mouse.GetState().LeftButton
            == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        if (!held || _journalMenu!.GetChildMenu() != null)
        {
            if (_journalDragging) _journalContext!.PersistJournalOffset();
            _journalDragging = false;
            _journalPendingDrag = false;
            return;
        }

        var cursor = Helper.Input.GetCursorPosition().GetScaledScreenPixels();
        if (!_journalDragging)
        {
            if (System.Math.Abs(cursor.X - _journalPressPos.X)
                + System.Math.Abs(cursor.Y - _journalPressPos.Y) <= JournalDragThreshold)
                return;
            _journalDragging = true;
        }

        _journalContext!.SetJournalTopLeft(new Microsoft.Xna.Framework.Point(
            (int)(cursor.X - _journalGrab.X),
            (int)(cursor.Y - _journalGrab.Y)));
        // Now that it's a real drag, keep the held button from reaching the menu.
        Helper.Input.Suppress(SButton.MouseLeft);
    }

    // Serve the journal's theme colours as a Content Patcher-editable data
    // asset (a string->hex dictionary). Authors override entries with EditData.
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(JournalTheme.AssetName(ModManifest.UniqueID)))
            e.LoadFrom(JournalTheme.BuildDefaults, AssetLoadPriority.Low);
    }

    // Re-read the theme when a patch invalidates it, and repaint an open journal
    // so authors iterating with hot-reloaded CP packs see changes immediately.
    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        string themeName = JournalTheme.AssetName(ModManifest.UniqueID);
        bool hit = false;
        foreach (var name in e.NamesWithoutLocale)
        {
            if (name.IsEquivalentTo(themeName)) { hit = true; break; }
        }
        if (!hit) return;
        JournalTheme.Reload(Helper, ModManifest.UniqueID);
        _journalContext?.RefreshTheme();
    }

    // StardewUI's ViewMenu hides the corner HUD on construct. A per-tick reset
    // is needed because something also stomps displayHUD between our override
    // and the first frame draw on initial open.
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_journalMenu == null) return;
        if (ReferenceEquals(Game1.activeClickableMenu, _journalMenu))
        {
            Game1.displayHUD = true;
            if (_journalContext != null)
            {
                UpdateJournalDrag();
                // StardewUI only consults PositionSelector when the view
                // re-measures, which a drag doesn't trigger. Push the position
                // onto the menu every tick so the move actually applies.
                var topLeft = _journalContext.GetJournalTopLeft();
                _journalMenu.xPositionOnScreen = topLeft.X;
                _journalMenu.yPositionOnScreen = topLeft.Y;
            }
        }
        else
        {
            _journalDragging = false;
            _journalPendingDrag = false;
            if (Game1.activeClickableMenu == null)
                _journalMenu = null;
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _viewEngine = Helper.ModRegistry.GetApi<IViewEngine>("focustense.StardewUI");
        if (_viewEngine == null)
        {
            Monitor.Log("StardewUI not loaded. The journal can't render. Install focustense.StardewUI.", LogLevel.Error);
            return;
        }
        _viewEngine.RegisterViews(_viewPrefix, "assets/views");
        _viewEngine.RegisterSprites($"Mods/{ModManifest.UniqueID}/Sprites", "assets/sprites");

        // Pull theme colours now so the first journal open is already themed.
        JournalTheme.Reload(Helper, ModManifest.UniqueID);

        // MoreQuestsFramework is optional. Resolve once; null when not loaded.
        // Reward itemisation falls back to vanilla synthesis in that case.
        _mqfApi = Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (_mqfApi == null)
            Monitor.Log("MoreQuestsFramework not loaded. Reward itemisation will use vanilla synthesis only.", LogLevel.Trace);

        // The watcher captures natural completions (delivery, fishing, slay,
        // etc.) into CompletedQuestStore so the Completed tab isn't limited
        // to journal-Complete-button history.
        _completionWatcher = new CompletionWatcher(Helper, _mqfApi);
        _completionWatcher.Register();

        // Top-right HUD stack for pinned quests. Reads ShowHudPin per-frame, so a
        // GMCM toggle takes effect without re-registering.
        _pinnedHud = new PinnedObjectiveHud(Helper, _mqfApi);
        _pinnedHud.Register();

        // Auto-pin newly accepted quests when the player opts in.
        _newQuestPinner = new NewQuestPinner(Helper);
        _newQuestPinner.Register();
        if (Config.HotReloadViews)
            _viewEngine.EnableHotReloading();
        _viewEngine.PreloadAssets();

        if (Config.AddGameMenuTab)
        {
            // Prefer Better Game Menu's RegisterTab API when BGM is loaded.
            // BGM replaces GameMenu wholesale, so the floating-overlay tab
            // approach Ferngill uses can't reach BGM's tab strip. When BGM
            // isn't around, fall back to the overlay for vanilla GameMenu.
            var bgm = Helper.ModRegistry.GetApi<IBetterGameMenuApi>("leclair.bettergamemenu");
            if (bgm != null)
            {
                new BgmIntegration(
                    bgm,
                    BuildJournalMenu,
                    () => Helper.Translation.Get("journal.tab.tooltip").Default("Quest Journal").ToString()
                ).Register();
                Monitor.Log("Registered Quest Journal as a Better Game Menu tab.", LogLevel.Trace);
            }
            else
            {
                _tabOverlay = new GameMenuTabOverlay(
                    Helper,
                    BuildJournalMenu,
                    Helper.Translation.Get("journal.tab.tooltip").Default("Quest Journal").ToString());
                _tabOverlay.Register();
            }
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsPlayerFree && Game1.activeClickableMenu is not GameMenu)
            return;
        if (!Config.OpenJournalKey.JustPressed())
            return;
        if (_viewEngine == null)
            return;

        // Hotkey ignored while another non-GameMenu menu is open so it doesn't
        // ambush dialogues, shops, etc.
        if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not GameMenu)
            return;

        var journal = BuildJournalMenu();
        if (journal != null)
            Game1.activeClickableMenu = journal;
    }

    private IClickableMenu? BuildJournalMenu()
    {
        if (_viewEngine == null) return null;
        var ctx = new JournalContext(Helper, _viewEngine, _mqfApi, _viewPrefix, _completionWatcher);
        _journalContext = ctx;
        ctx.Refresh();
        // Zero the dim underlay so the game world stays visible behind the journal.
        var controller = _viewEngine.CreateMenuControllerFromAsset($"{_viewPrefix}/journal", ctx);
        controller.DimmingAmount = 0f;
        // Drive the window position from the context so the title-bar drag can
        // move it (and the offset persists across opens).
        controller.PositionSelector = () => ctx.GetJournalTopLeft();
        var menu = controller.Menu;
        controller.Closed += () =>
        {
            if (ReferenceEquals(_journalMenu, menu))
                _journalMenu = null;
            controller.Dispose();
        };
        _journalMenu = menu;
        return menu;
    }

    // Opens the journal and selects the quest matching a pin key. Called by the
    // HUD when the player clicks a pinned quest.
    internal void OpenJournalToQuest(string key)
    {
        if (_viewEngine == null) return;
        var journal = BuildJournalMenu();
        if (journal == null) return;
        Game1.activeClickableMenu = journal;
        _journalContext?.SelectQuestByKey(key);
    }

}
