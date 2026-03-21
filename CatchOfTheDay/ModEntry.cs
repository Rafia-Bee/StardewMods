#nullable enable
using System;
using System.Linq;
using CatchOfTheDay.Framework;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace CatchOfTheDay;

public class ModEntry : Mod
{
    private WeatherFishHud _hud = null!;
    private ModConfig _config = null!;
    private readonly FishHudOverlay _overlay = new();
    private IGenericModConfigMenuApi? _gmcmApi;
    private IGMCMOptionsAPI? _colorApi;

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
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
    }

    private void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer)
            _hud.Refresh();
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !_config.Enabled)
            return;

        if (!_config.HideFishKey.JustPressed())
            return;

        string? fishId = _hud.HoveredFishId;
        string? fishName = _hud.HoveredFishName;
        if (fishId == null || fishName == null)
            return;

        if (_config.HiddenFishIds.Contains(fishId))
        {
            _config.HiddenFishIds.Remove(fishId);
            Game1.addHUDMessage(new HUDMessage($"{fishName} is no longer hidden from the fishing HUD."));
        }
        else
        {
            _config.HiddenFishIds.Add(fishId);
            Game1.addHUDMessage(new HUDMessage($"{fishName} hidden from the fishing HUD."));
        }

        Helper.WriteConfig(_config);
        _hud.Refresh();
        RegisterGmcm();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        _colorApi = Helper.ModRegistry.GetApi<IGMCMOptionsAPI>("jltaylor-us.GMCMOptions");
        RegisterGmcm();
    }

    private void RegisterGmcm()
    {
        var api = _gmcmApi;
        if (api == null)
            return;

        api.Unregister(ModManifest);

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

        api.AddKeybindList(
            ModManifest,
            () => _config.HideFishKey,
            v => _config.HideFishKey = v,
            () => Helper.Translation.Get("config.hide-fish-key.name"),
            () => Helper.Translation.Get("config.hide-fish-key.tooltip")
        );

        if (_config.HiddenFishIds.Count > 0)
        {
            api.AddPageLink(
                ModManifest,
                "hidden-fish",
                () => Helper.Translation.Get("config.page-hidden-fish.name"),
                () => Helper.Translation.Get("config.page-hidden-fish.tooltip",
                    new { count = _config.HiddenFishIds.Count })
            );
        }

        api.AddPageLink(
            ModManifest,
            "time-slots",
            () => Helper.Translation.Get("config.page-time-slots.name"),
            () => Helper.Translation.Get("config.page-time-slots.tooltip")
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

        var colorApi = _colorApi;
        if (colorApi != null)
        {
            colorApi.AddColorOption(
                ModManifest,
                () => ParseHexColor(_config.CatchableNowColor),
                v => _config.CatchableNowColor = ColorToHex(v),
                () => Helper.Translation.Get("config.catchable-now-color.name"),
                () => Helper.Translation.Get("config.catchable-now-color.tooltip"),
                showAlpha: true,
                colorPickerStyle: 1
            );
        }
        else
        {
            api.AddTextOption(
                ModManifest,
                () => _config.CatchableNowColor,
                v => _config.CatchableNowColor = v,
                () => Helper.Translation.Get("config.catchable-now-color.name"),
                () => Helper.Translation.Get("config.catchable-now-color.tooltip")
            );
        }

        // Time Slots subpage
        api.AddPage(
            ModManifest,
            "time-slots",
            () => Helper.Translation.Get("config.page-time-slots.name")
        );

        for (int i = 0; i < _config.TimeSlots.Count; i++)
        {
            int idx = i;
            int num = i + 1;

            api.AddTextOption(
                ModManifest,
                () => _config.TimeSlots[idx].TimeRanges,
                v => _config.TimeSlots[idx].TimeRanges = v,
                () => Helper.Translation.Get("config.time-slot-ranges.name", new { num }),
                () => Helper.Translation.Get("config.time-slot-ranges.tooltip")
            );

            if (colorApi != null)
            {
                colorApi.AddColorOption(
                    ModManifest,
                    () => ParseHexColor(_config.TimeSlots[idx].Color),
                    v => _config.TimeSlots[idx].Color = ColorToHex(v),
                    () => Helper.Translation.Get("config.time-slot-color.name", new { num }),
                    () => Helper.Translation.Get("config.time-slot-color.tooltip"),
                    showAlpha: true,
                    colorPickerStyle: 1
                );
            }
            else
            {
                api.AddTextOption(
                    ModManifest,
                    () => _config.TimeSlots[idx].Color,
                    v => _config.TimeSlots[idx].Color = v,
                    () => Helper.Translation.Get("config.time-slot-color.name", new { num }),
                    () => Helper.Translation.Get("config.time-slot-color.tooltip")
                );
            }
        }

        // Hidden Fish subpage
        if (_config.HiddenFishIds.Count > 0)
        {
            api.AddPage(
                ModManifest,
                "hidden-fish",
                () => Helper.Translation.Get("config.page-hidden-fish.name")
            );

            foreach (string fishId in _config.HiddenFishIds.ToList())
            {
                string displayName = ItemRegistry.GetData(fishId)?.DisplayName ?? fishId;
                string capturedId = fishId;

                api.AddBoolOption(
                    ModManifest,
                    () => _config.HiddenFishIds.Contains(capturedId),
                    v =>
                    {
                        if (!v)
                            _config.HiddenFishIds.Remove(capturedId);
                        else if (!_config.HiddenFishIds.Contains(capturedId))
                            _config.HiddenFishIds.Add(capturedId);
                    },
                    () => displayName,
                    () => Helper.Translation.Get("config.hidden-fish-entry.tooltip",
                        new { fish = displayName })
                );
            }
        }
    }

    private static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
            return Color.Transparent;
        hex = hex.Substring(1);
        if (hex.Length == 6) hex += "FF";
        if (hex.Length != 8) return Color.Transparent;
        try
        {
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            int a = Convert.ToInt32(hex.Substring(6, 2), 16);
            return new Color(r, g, b, a);
        }
        catch { return Color.Transparent; }
    }

    private static string ColorToHex(Color c)
    {
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
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
