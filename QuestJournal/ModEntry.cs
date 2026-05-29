using QuestJournal.Api;
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
    private IClickableMenu? _journalMenu;
    private JournalContext? _journalContext;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();
        _viewPrefix = $"Mods/{ModManifest.UniqueID}/Views";

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
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
            Game1.displayHUD = true;
        else if (Game1.activeClickableMenu == null)
            _journalMenu = null;
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

}
