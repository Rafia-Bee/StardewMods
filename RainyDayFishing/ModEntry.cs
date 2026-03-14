using RainyDayFishing.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace RainyDayFishing;

public class ModEntry : Mod
{
    private RainyFishHud _hud = null!;
    private ModConfig _config = null!;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _hud = new RainyFishHud(helper, Monitor, () => _config);

        helper.Events.GameLoop.DayStarted += (_, _) => _hud.Refresh();
        helper.Events.Player.Warped += OnPlayerWarped;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => _hud.Clear();
        helper.Events.Display.RenderedHud += (_, e) => _hud.Draw(e.SpriteBatch);
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnPlayerWarped(object sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer)
            _hud.Refresh();
    }

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            ModManifest,
            () => _config = new ModConfig(),
            () => Helper.WriteConfig(_config)
        );

        api.AddBoolOption(
            ModManifest,
            () => _config.Enabled,
            v => _config.Enabled = v,
            () => Helper.Translation.Get("config.enabled.name"),
            () => Helper.Translation.Get("config.enabled.tooltip")
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
            () => _config.MaxLocations,
            v => _config.MaxLocations = v,
            () => Helper.Translation.Get("config.max-locations.name"),
            () => Helper.Translation.Get("config.max-locations.tooltip"),
            min: 0, max: 20
        );
    }
}
