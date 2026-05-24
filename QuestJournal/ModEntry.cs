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
    private string _viewPrefix = null!;
    private GameMenuTabOverlay? _tabOverlay;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();
        _viewPrefix = $"Mods/{ModManifest.UniqueID}/Views";

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
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
        // Sprite registration deferred: we don't ship custom sprites yet, and
        // StardewUI's preloader throws DirectoryNotFoundException when the
        // assets/sprites folder doesn't exist. Re-enable in step 13's art pass.
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
        var ctx = new JournalContext();
        ctx.Refresh();
        return _viewEngine.CreateMenuFromAsset($"{_viewPrefix}/journal", ctx);
    }

}
