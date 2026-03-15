#nullable enable
using CatchOfTheDay.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace CatchOfTheDay;

public class ModEntry : Mod
{
    private WeatherFishHud _hud = null!;
    private ModConfig _config = null!;
    private readonly FishHudOverlay _overlay = new();

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _hud = new WeatherFishHud(helper, Monitor, () => _config, _overlay);

        helper.Events.GameLoop.DayStarted += (_, _) => _hud.Refresh();
        helper.Events.Player.Warped += OnPlayerWarped;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.Display.RenderedHud += (_, e) => _hud.Draw(e.SpriteBatch);
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer)
            _hud.Refresh();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            ModManifest,
            () => _config = new ModConfig(),
            () => { Helper.WriteConfig(_config); _hud.Refresh(); }
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.Enabled,
            v => _config.Enabled = v,
            () => Helper.Translation.Get("config.enabled.name"),
            () => Helper.Translation.Get("config.enabled.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.ShowBundleNeeds,
            v => _config.ShowBundleNeeds = v,
            () => Helper.Translation.Get("config.show-bundle-needs.name"),
            () => Helper.Translation.Get("config.show-bundle-needs.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.HideAlreadyCaught,
            v => _config.HideAlreadyCaught = v,
            () => Helper.Translation.Get("config.hide-already-caught.name"),
            () => Helper.Translation.Get("config.hide-already-caught.tooltip")
        );

        api.AddNumberOption(
            ModManifest,
            () => _config.MinSellPrice,
            v => _config.MinSellPrice = v,
            () => Helper.Translation.Get("config.min-sell-price.name"),
            () => Helper.Translation.Get("config.min-sell-price.tooltip"),
            min: 0, max: 5000, interval: 25
        );

        api.AddPageLink(
            ModManifest,
            "position",
            () => Helper.Translation.Get("config.page-position.name"),
            () => Helper.Translation.Get("config.page-position.tooltip")
        );

        api.AddSectionTitle(
            ModManifest,
            () => Helper.Translation.Get("config.weather-section.name"),
            () => Helper.Translation.Get("config.weather-section.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.TrackRain,
            v => _config.TrackRain = v,
            () => Helper.Translation.Get("config.track-rain.name"),
            () => Helper.Translation.Get("config.track-rain.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.TrackStorm,
            v => _config.TrackStorm = v,
            () => Helper.Translation.Get("config.track-storm.name"),
            () => Helper.Translation.Get("config.track-storm.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.TrackSun,
            v => _config.TrackSun = v,
            () => Helper.Translation.Get("config.track-sun.name"),
            () => Helper.Translation.Get("config.track-sun.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.TrackSnow,
            v => _config.TrackSnow = v,
            () => Helper.Translation.Get("config.track-snow.name"),
            () => Helper.Translation.Get("config.track-snow.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.TrackWind,
            v => _config.TrackWind = v,
            () => Helper.Translation.Get("config.track-wind.name"),
            () => Helper.Translation.Get("config.track-wind.tooltip")
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.TrackGreenRain,
            v => _config.TrackGreenRain = v,
            () => Helper.Translation.Get("config.track-green-rain.name"),
            () => Helper.Translation.Get("config.track-green-rain.tooltip")
        );

        // Position & Layout subpage
        api.AddPage(
            ModManifest,
            "position",
            () => Helper.Translation.Get("config.page-position.name")
        );

        api.AddNumberOption(
            ModManifest,
            () => _config.HudX,
            v => _config.HudX = v,
            () => Helper.Translation.Get("config.hud-x.name"),
            () => Helper.Translation.Get("config.hud-x.tooltip"),
            min: -4000, max: 4000
        );

        api.AddNumberOption(
            ModManifest,
            () => _config.HudY,
            v => _config.HudY = v,
            () => Helper.Translation.Get("config.hud-y.name"),
            () => Helper.Translation.Get("config.hud-y.tooltip"),
            min: 0, max: 4000
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.HorizontalLayout,
            v => _config.HorizontalLayout = v,
            () => Helper.Translation.Get("config.horizontal-layout.name"),
            () => Helper.Translation.Get("config.horizontal-layout.tooltip")
        );

        api.AddNumberOption(
            ModManifest,
            () => _config.IconScale,
            v => _config.IconScale = v,
            () => Helper.Translation.Get("config.icon-scale.name"),
            () => Helper.Translation.Get("config.icon-scale.tooltip"),
            min: 0.5f, max: 3.0f, interval: 0.25f
        );

        api.AddNumberOption(
            ModManifest,
            () => _config.MaxLocations,
            v => _config.MaxLocations = v,
            () => Helper.Translation.Get("config.max-locations.name"),
            () => Helper.Translation.Get("config.max-locations.tooltip"),
            min: 0, max: 20
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!Game1.onScreenMenus.Contains(_overlay))
            Game1.onScreenMenus.Add(_overlay);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _hud.Clear();
        Game1.onScreenMenus.Remove(_overlay);
        _overlay.HoveredItem = null;
    }
}
