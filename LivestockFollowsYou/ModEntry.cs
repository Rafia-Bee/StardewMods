using System;
using LivestockFollowsYou.Framework;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace LivestockFollowsYou;

/// <summary>Main entry point for the Livestock Follows You mod.</summary>
public class ModEntry : Mod
{
    private ModConfig Config;
    private AnimalFollowManager FollowManager;
    private NpcReactionManager NpcReactions;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        FollowManager = new AnimalFollowManager(Monitor, helper, () => Config);
        NpcReactions = new NpcReactionManager(Monitor, helper, () => Config);

        // Wire up Harmony patches
        PurchasePatches.Manager = FollowManager;
        PurchasePatches.GetConfig = () => Config;
        PurchasePatches.Monitor = Monitor;

        var harmony = new Harmony(ModManifest.UniqueID);
        PurchasePatches.Apply(harmony);

        // SMAPI events
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.Player.Warped += OnPlayerWarped;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
    }

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm == null)
            return;

        gmcm.Register(
            mod: ModManifest,
            reset: () => Config = new ModConfig(),
            save: () => Helper.WriteConfig(Config)
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.Enabled,
            setValue: v => Config.Enabled = v,
            name: () => Helper.Translation.Get("config.enabled.name"),
            tooltip: () => Helper.Translation.Get("config.enabled.tooltip")
        );

        gmcm.AddSectionTitle(
            mod: ModManifest,
            text: () => "Movement"
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.FollowSpeedMultiplier,
            setValue: v => Config.FollowSpeedMultiplier = v,
            name: () => Helper.Translation.Get("config.follow_speed.name"),
            tooltip: () => Helper.Translation.Get("config.follow_speed.tooltip"),
            min: 0.5f,
            max: 3.0f,
            interval: 0.25f
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.RubberBandDistance,
            setValue: v => Config.RubberBandDistance = v,
            name: () => Helper.Translation.Get("config.rubber_band_distance.name"),
            tooltip: () => Helper.Translation.Get("config.rubber_band_distance.tooltip"),
            min: 5,
            max: 20
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.AutoDeliverTime,
            setValue: v => Config.AutoDeliverTime = v,
            name: () => Helper.Translation.Get("config.auto_deliver_time.name"),
            tooltip: () => Helper.Translation.Get("config.auto_deliver_time.tooltip"),
            min: 1200,
            max: 2400,
            interval: 100,
            formatValue: FormatGameTime
        );

        gmcm.AddSectionTitle(
            mod: ModManifest,
            text: () => "Audio & Notifications"
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.AnimalSoundsWhileFollowing,
            setValue: v => Config.AnimalSoundsWhileFollowing = v,
            name: () => Helper.Translation.Get("config.animal_sounds.name"),
            tooltip: () => Helper.Translation.Get("config.animal_sounds.tooltip")
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.SoundIntervalSeconds,
            setValue: v => Config.SoundIntervalSeconds = v,
            name: () => Helper.Translation.Get("config.sound_interval.name"),
            tooltip: () => Helper.Translation.Get("config.sound_interval.tooltip"),
            min: 5,
            max: 60
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.ShowNotifications,
            setValue: v => Config.ShowNotifications = v,
            name: () => Helper.Translation.Get("config.show_notifications.name"),
            tooltip: () => Helper.Translation.Get("config.show_notifications.tooltip")
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.NpcReactionsEnabled,
            setValue: v => Config.NpcReactionsEnabled = v,
            name: () => Helper.Translation.Get("config.npc_reactions.name"),
            tooltip: () => Helper.Translation.Get("config.npc_reactions.tooltip")
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.DebugLogging,
            setValue: v => Config.DebugLogging = v,
            name: () => Helper.Translation.Get("config.debug_logging.name"),
            tooltip: () => Helper.Translation.Get("config.debug_logging.tooltip")
        );
    }

    private void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
    {
        if (!Context.IsWorldReady || !FollowManager.HasFollowers)
            return;

        FollowManager.UpdateMovement(Game1.currentGameTime);
        NpcReactions.Update(FollowManager.Followers);
    }

    private void OnTimeChanged(object sender, TimeChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !FollowManager.HasFollowers)
            return;

        // Check auto-delivery on time change as a secondary trigger
        if (e.NewTime >= Config.AutoDeliverTime)
            FollowManager.DeliverAll();
    }

    private void OnPlayerWarped(object sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer || !FollowManager.HasFollowers)
            return;

        FollowManager.OnPlayerWarped(e.OldLocation, e.NewLocation);
    }

    private void OnDayEnding(object sender, DayEndingEventArgs e)
    {
        FollowManager.DeliverAll();
        NpcReactions.Reset();
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        if (Game1.IsMasterGame)
            FollowManager.CleanupStrays();
    }

    private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        FollowManager.Reset();
        NpcReactions.Reset();
    }

    private static string FormatGameTime(int time)
    {
        int hours = time / 100;
        int minutes = time % 100;
        string amPm = hours >= 12 ? "PM" : "AM";
        if (hours > 12) hours -= 12;
        if (hours == 0) hours = 12;
        return $"{hours}:{minutes:D2} {amPm}";
    }
}
