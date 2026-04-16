#nullable enable
using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using UIInfoSuiteAddon.Framework;

namespace UIInfoSuiteAddon;

public class ModEntry : Mod
{
    private readonly BirthdayLookupOverlay _overlay = new();
    private ModConfig _config = new();

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _overlay.GetConfig = () => _config;
        _overlay.Translate = key => helper.Translation.Get(key).ToString();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Display.RenderedHud += OnRenderedHud;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var harmony = new Harmony(ModManifest.UniqueID);
        if (!BirthdayIconPatch.Apply(harmony, Monitor))
            Monitor.Log("Birthday lookup integration could not be initialized.", LogLevel.Warn);

        SetupGMCM();
    }

    private void SetupGMCM()
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            mod: ModManifest,
            reset: () => _config = new ModConfig(),
            save: () => Helper.WriteConfig(_config)
        );

        api.AddNumberOption(
            mod: ModManifest,
            getValue: () => _config.MaxLovedGiftsToShow,
            setValue: val => _config.MaxLovedGiftsToShow = val,
            name: () => Helper.Translation.Get("config.max-loved-gifts.name").ToString(),
            tooltip: () => Helper.Translation.Get("config.max-loved-gifts.tooltip").ToString(),
            min: 1,
            max: 20
        );

        api.AddBoolOption(
            mod: ModManifest,
            getValue: () => _config.ExcludeUniversalLoves,
            setValue: val => _config.ExcludeUniversalLoves = val,
            name: () => Helper.Translation.Get("config.exclude-universal-loves.name").ToString(),
            tooltip: () => Helper.Translation.Get("config.exclude-universal-loves.tooltip").ToString()
        );

        api.AddBoolOption(
            mod: ModManifest,
            getValue: () => _config.OnlyShowOwnedGifts,
            setValue: val => _config.OnlyShowOwnedGifts = val,
            name: () => Helper.Translation.Get("config.only-show-owned-gifts.name").ToString(),
            tooltip: () => Helper.Translation.Get("config.only-show-owned-gifts.tooltip").ToString()
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!Game1.onScreenMenus.Contains(_overlay))
            Game1.onScreenMenus.Add(_overlay);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        Game1.onScreenMenus.Remove(_overlay);
        _overlay.HoveredNpc = null;
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (BirthdayIconPatch.LastUpdateTick != Game1.ticks)
        {
            _overlay.HoveredNpc = null;
            return;
        }

        var npcs = BirthdayIconPatch.CurrentNPCs;
        var icons = BirthdayIconPatch.CurrentIcons;

        if (npcs == null || icons == null || npcs.Count == 0 || icons.Count == 0)
        {
            _overlay.HoveredNpc = null;
            return;
        }

        int mouseX = Game1.getMouseX();
        int mouseY = Game1.getMouseY();

        NPC? hovered = null;
        int count = Math.Min(npcs.Count, icons.Count);
        for (int i = 0; i < count; i++)
        {
            if (icons[i].containsPoint(mouseX, mouseY))
            {
                hovered = npcs[i];
                break;
            }
        }

        _overlay.HoveredNpc = hovered;
    }
}
