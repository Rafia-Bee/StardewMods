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
    internal static ModConfig Config { get; set; } = new();

    // Chatty logs (anything below a warning) only show when the player turns on
    // debug logging. Warnings and errors always go through Monitor.Log directly.
    internal static void DebugLog(string message, LogLevel level = LogLevel.Trace)
    {
        if (Config.DebugLogging)
            Instance?.Monitor.Log(message, level);
    }

    private IViewEngine? _viewEngine;
    private IMoreQuestsApi? _mqfApi;
    private string _viewPrefix = null!;
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
        // Controller tab switching and custom-tab editing run off configurable
        // keybinds in OnButtonsChanged (KeybindList supports multi-key binds and
        // GMCM rebinding). This handler is just the mouse drag-to-move arming.
        if (e.Button != SButton.MouseLeft) return;
        var frame = JournalFrameRect();
        if (frame == null) return;
        var cursor = CursorUtil.UiSpace(e.Cursor);
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

        var cursor = CursorUtil.UiSpace(Helper.Input.GetCursorPosition());
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

    // Content path the journal icon is served at. Iconic Framework loads its
    // icon textures through the game content pipeline, so the icon needs a
    // content name rather than just the raw mod file.
    private string IconAssetName => $"Mods/{ModManifest.UniqueID}/Icon";

    // Serve the journal's theme colours as a Content Patcher-editable data
    // asset (a string->hex dictionary). Authors override entries with EditData.
    // Also serves the toolbar icon texture so Iconic Framework can load it.
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(JournalTheme.AssetName(ModManifest.UniqueID)))
            e.LoadFrom(JournalTheme.BuildDefaults, AssetLoadPriority.Low);
        else if (e.NameWithoutLocale.IsEquivalentTo(IconAssetName))
            e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>(
                "assets/sprites/menuIcon.png", AssetLoadPriority.Medium);
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
            DebugLog("MoreQuestsFramework not loaded. Reward itemisation will use vanilla synthesis only.");

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

        GmcmRegistration.Register(Helper, ModManifest);

        // Register a toolbar icon with Iconic Framework when it's installed.
        // This is also how the journal reaches controller players: Star Control
        // reads Iconic Framework's icons into its radial menu, so the same icon
        // opens the journal from a gamepad without needing a bound key.
        IconicFrameworkIntegration.Register(
            Helper,
            ModManifest,
            IconAssetName,
            () => Helper.Translation.Get("journal.tab.tooltip").Default("Quest Journal").ToString(),
            () => Helper.Translation.Get("iconic.description").Default("Open the quest journal.").ToString(),
            OpenJournal);

        if (Config.AddGameMenuTab)
        {
            // The journal can only ride the Esc menu's tab strip through Better
            // Game Menu's tab API. Without BGM we'd have to float our own tab
            // over the vanilla GameMenu, which lands in the same top-right corner
            // other tab mods use (UI Info Suite 2, etc.) and ends up stealing
            // their clicks. So when BGM isn't installed we skip the tab entirely
            // and leave the journal on the F6 hotkey and the Iconic Framework icon.
            var bgm = Helper.ModRegistry.GetApi<IBetterGameMenuApi>("leclair.bettergamemenu");
            if (bgm != null)
            {
                var menuIcon = Helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("assets/sprites/menuIcon.png");
                new BgmIntegration(
                    bgm,
                    BuildJournalMenu,
                    () => Helper.Translation.Get("journal.tab.tooltip").Default("Quest Journal").ToString(),
                    menuIcon
                ).Register();
                DebugLog("Registered Quest Journal as a Better Game Menu tab.");
            }
            else
            {
                Monitor.Log(
                    "Better Game Menu isn't installed, so the Esc-menu tab is off (it would clash with other menu-tab mods). Open the journal with the F6 hotkey or the Iconic Framework icon instead.",
                    LogLevel.Info);
            }
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        // In-journal controller shortcuts. The tab rail and the "+"/"Edit"
        // controls float above the frame, so a gamepad can't focus them; these
        // configurable binds drive tab switching and custom-tab editing instead.
        // Only while the journal is the active menu with no child popup open.
        if (_journalContext != null && _journalMenu != null
            && ReferenceEquals(Game1.activeClickableMenu, _journalMenu)
            && _journalMenu.GetChildMenu() == null)
        {
            if (Config.NextTabKey.JustPressed())
            {
                _journalContext.NextTab();
                Game1.playSound("smallSelect");
                Helper.Input.SuppressActiveKeybinds(Config.NextTabKey);
                return;
            }
            if (Config.PrevTabKey.JustPressed())
            {
                _journalContext.PrevTab();
                Game1.playSound("smallSelect");
                Helper.Input.SuppressActiveKeybinds(Config.PrevTabKey);
                return;
            }
            if (Config.EditTabKey.JustPressed() && _journalContext.CanEditActiveTab)
            {
                _journalContext.EditActiveTab();
                Game1.playSound("smallSelect");
                Helper.Input.SuppressActiveKeybinds(Config.EditTabKey);
                return;
            }
            if (Config.AddTabKey.JustPressed())
            {
                _journalContext.CreateTab();
                Game1.playSound("smallSelect");
                Helper.Input.SuppressActiveKeybinds(Config.AddTabKey);
                return;
            }
        }

        if (!Context.IsPlayerFree && Game1.activeClickableMenu is not GameMenu)
            return;
        if (!Config.OpenJournalKey.JustPressed())
            return;
        OpenJournal();
    }

    // Shared open path used by the hotkey and the Iconic Framework / Star
    // Control icon. Replaces an open GameMenu, but won't ambush another menu
    // (dialogue, shop, etc.) or fire when the view engine is missing.
    internal void OpenJournal()
    {
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
