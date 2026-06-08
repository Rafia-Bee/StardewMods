using QuestJournal.Api;
using QuestJournal.Hud;
using QuestJournal.Integrations;
using QuestJournal.Menu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace QuestJournal;

// The mod's entry point. Wires up SMAPI events, loads config and assets, and hooks into
// StardewUI plus the other mods we integrate with (MoreQuests, Iconic, Better Game Menu).
// Also handles opening the journal menu and letting the player drag it around the screen.
public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    internal static void DebugLog(string message, LogLevel level = LogLevel.Trace)
    {
        if (Config.DebugLogging)
            Instance?.Monitor.Log(message, level);
    }

    private IViewEngine? _viewEngine;
    private IMoreQuestsApi? _mqfApi;
    private string _viewPrefix = null!;
    private readonly ExternalEntryRegistry _externalEntries = new();
    private CompletionWatcher? _completionWatcher;
    private PinnedObjectiveHud? _pinnedHud;
    private NewQuestPinner? _newQuestPinner;
    private IClickableMenu? _journalMenu;
    private JournalContext? _journalContext;

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

        RegisterConsoleCommands(helper);
    }

    public override object GetApi() => new QuestJournalApi(_externalEntries);

    private void RegisterConsoleCommands(IModHelper helper)
    {
        helper.ConsoleCommands.Add(
            "qj_addtest",
            "Add a test external journal entry through the Quest Journal API. Usage: qj_addtest [key] [title...]",
            (_, args) =>
            {
                string key = args.Length > 0 ? args[0] : "test1";
                string title = args.Length > 1
                    ? string.Join(" ", args, 1, args.Length - 1)
                    : "Find 5 strawberries";
                const string owner = "RafiaBee.QuestJournal.Test";
                _externalEntries.AddOrUpdate(new Api.JournalEntry
                {
                    OwnerId = owner,
                    Key = key,
                    Title = title,
                    Description = "A test to-do registered through the Quest Journal API.",
                    Objective = title,
                    Source = "Test",
                    Category = "Personal",
                    DeadlineDays = 3,
                    OnComplete = () => _externalEntries.Remove(owner, key),
                    OnCancel = () => _externalEntries.Remove(owner, key)
                });
                Monitor.Log($"Added test entry '{key}'. Open the journal and check the Active tab.", LogLevel.Info);
            });

        helper.ConsoleCommands.Add(
            "qj_cleartest",
            "Remove all test external journal entries added by qj_addtest.",
            (_, _) =>
            {
                _externalEntries.Clear("RafiaBee.QuestJournal.Test");
                Monitor.Log("Cleared test external entries.", LogLevel.Info);
            });
    }

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
        var cursor = CursorUtil.UiSpace(e.Cursor);
        if (!frame.Value.Contains((int)cursor.X, (int)cursor.Y)) return;
        var tl = _journalContext!.GetJournalTopLeft();
        _journalPendingDrag = true;
        _journalPressPos = cursor;
        _journalGrab = new Microsoft.Xna.Framework.Vector2(cursor.X - tl.X, cursor.Y - tl.Y);
    }

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
        Helper.Input.Suppress(SButton.MouseLeft);
    }

    private string IconAssetName => $"Mods/{ModManifest.UniqueID}/Icon";

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(JournalTheme.AssetName(ModManifest.UniqueID)))
            e.LoadFrom(JournalTheme.BuildDefaults, AssetLoadPriority.Low);
        else if (e.NameWithoutLocale.IsEquivalentTo(IconAssetName))
            e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>(
                "assets/sprites/menuIcon.png", AssetLoadPriority.Medium);
    }

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

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_journalMenu == null) return;
        if (ReferenceEquals(Game1.activeClickableMenu, _journalMenu))
        {
            Game1.displayHUD = true;
            if (_journalContext != null)
            {
                UpdateJournalDrag();
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

        JournalTheme.Reload(Helper, ModManifest.UniqueID);

        _mqfApi = Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (_mqfApi == null)
            DebugLog("MoreQuestsFramework not loaded. Reward itemisation will use vanilla synthesis only.");

        _completionWatcher = new CompletionWatcher(Helper, _mqfApi);
        _completionWatcher.Register();

        _pinnedHud = new PinnedObjectiveHud(Helper, _mqfApi, _externalEntries);
        _pinnedHud.Register();

        _newQuestPinner = new NewQuestPinner(Helper);
        _newQuestPinner.Register();
        if (Config.HotReloadViews)
            _viewEngine.EnableHotReloading();
        _viewEngine.PreloadAssets();

        GmcmRegistration.Register(Helper, ModManifest);

        IconicFrameworkIntegration.Register(
            Helper,
            ModManifest,
            IconAssetName,
            () => Helper.Translation.Get("journal.tab.tooltip").Default("Quest Journal").ToString(),
            () => Helper.Translation.Get("iconic.description").Default("Open the quest journal.").ToString(),
            OpenJournal);

        if (Config.AddGameMenuTab)
        {
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
            if (Config.TogglePinKey.JustPressed() && Game1.keyboardDispatcher.Subscriber == null)
            {
                _journalContext.PinSelected();
                Game1.playSound("smallSelect");
                Helper.Input.SuppressActiveKeybinds(Config.TogglePinKey);
                return;
            }
        }

        if (Config.ToggleHudKey.JustPressed() && Context.IsPlayerFree)
        {
            Config.ShowHudPin = !Config.ShowHudPin;
            Helper.WriteConfig(Config);
            Game1.addHUDMessage(HUDMessage.ForCornerTextbox(
                Helper.Translation.Get(Config.ShowHudPin ? "hud.toggle.on" : "hud.toggle.off")
                    .Default(Config.ShowHudPin ? "Pinned quests shown" : "Pinned quests hidden").ToString()));
            Helper.Input.SuppressActiveKeybinds(Config.ToggleHudKey);
            return;
        }

        if (!Context.IsPlayerFree && Game1.activeClickableMenu is not GameMenu)
            return;
        if (!Config.OpenJournalKey.JustPressed())
            return;
        OpenJournal();
    }

    internal void OpenJournal()
    {
        if (_viewEngine == null)
            return;

        if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not GameMenu)
            return;

        var journal = BuildJournalMenu();
        if (journal != null)
            Game1.activeClickableMenu = journal;
    }

    private IClickableMenu? BuildJournalMenu()
    {
        if (_viewEngine == null) return null;
        var ctx = new JournalContext(Helper, _viewEngine, _mqfApi, _viewPrefix, _completionWatcher, _externalEntries);
        _journalContext = ctx;
        ctx.Refresh();
        var controller = _viewEngine.CreateMenuControllerFromAsset($"{_viewPrefix}/journal", ctx);
        controller.DimmingAmount = 0f;
        controller.PositionSelector = () => ctx.GetJournalTopLeft();
        var menu = controller.Menu;
        controller.Closed += () =>
        {
            if (ReferenceEquals(_journalMenu, menu))
                _journalMenu = null;
            ctx.Detach();
            controller.Dispose();
        };
        _journalMenu = menu;
        return menu;
    }

    internal void OpenJournalToQuest(string key)
    {
        if (_viewEngine == null) return;
        var journal = BuildJournalMenu();
        if (journal == null) return;
        Game1.activeClickableMenu = journal;
        _journalContext?.SelectQuestByKey(key);
    }

}
