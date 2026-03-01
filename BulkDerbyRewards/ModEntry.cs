using HarmonyLib;
using BulkDerbyRewards.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BulkDerbyRewards;

/// <summary>Main entry point for the Bulk Derby Rewards mod.</summary>
public class ModEntry : Mod
{
    internal const string ModId = "RafiaBee.BulkDerbyRewards";
    internal static IMonitor ModMonitor;
    internal static IModHelper ModHelper;
    internal static ModConfig Config;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = Monitor;
        ModHelper = helper;
        Config = helper.ReadConfig<ModConfig>();

        var harmony = new Harmony(ModId);
        DerbyPatches.Apply(harmony);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        RegisterConfigMenu();
    }

    private void RegisterConfigMenu()
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            mod: ModManifest,
            reset: () => Config = new ModConfig(),
            save: () => Helper.WriteConfig(Config));

        api.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.Enabled,
            setValue: val => Config.Enabled = val,
            name: () => Helper.Translation.Get("config.enabled.name"),
            tooltip: () => Helper.Translation.Get("config.enabled.tooltip"));

        api.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.AlwaysBulk,
            setValue: val => Config.AlwaysBulk = val,
            name: () => Helper.Translation.Get("config.always_bulk.name"),
            tooltip: () => Helper.Translation.Get("config.always_bulk.tooltip"));
    }
}
