using System;
using System.Linq;
using LivestockFollowsYou.Framework;
using HarmonyLib;
using Microsoft.Xna.Framework;
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
    private GrazingManager GrazingMgr;
    private GrazingBellItem GrazingBell;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        FollowManager = new AnimalFollowManager(Monitor, helper, () => Config);
        NpcReactions = new NpcReactionManager(Monitor, helper, () => Config);
        GrazingMgr = new GrazingManager(Monitor, helper, () => Config);
        GrazingBell = new GrazingBellItem(helper);
        GrazingBell.Register();

        PurchasePatches.Manager = FollowManager;
        PurchasePatches.GetConfig = () => Config;
        PurchasePatches.Monitor = Monitor;

        var harmony = new Harmony(ModManifest.UniqueID);
        PurchasePatches.Apply(harmony);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.Player.Warped += OnPlayerWarped;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
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

        gmcm.AddSectionTitle(
            mod: ModManifest,
            text: () => "Grazing Bell"
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.GrazingHappinessBoost,
            setValue: v => Config.GrazingHappinessBoost = v,
            name: () => Helper.Translation.Get("config.grazing_happiness.name"),
            tooltip: () => Helper.Translation.Get("config.grazing_happiness.tooltip"),
            min: 5,
            max: 50
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.MinFriendshipToSendHome,
            setValue: v => Config.MinFriendshipToSendHome = v,
            name: () => Helper.Translation.Get("config.min_friendship_send_home.name"),
            tooltip: () => Helper.Translation.Get("config.min_friendship_send_home.tooltip"),
            min: 0,
            max: 1000,
            interval: 50
        );

        gmcm.AddNumberOption(
            mod: ModManifest,
            getValue: () => Config.GrazingIdleSeconds,
            setValue: v => Config.GrazingIdleSeconds = v,
            name: () => Helper.Translation.Get("config.grazing_idle_seconds.name"),
            tooltip: () => Helper.Translation.Get("config.grazing_idle_seconds.tooltip"),
            min: 1,
            max: 5
        );
    }

    private void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
    {
        if (!Context.IsWorldReady || !FollowManager.HasFollowers)
            return;

        FollowManager.UpdateMovement(Game1.currentGameTime);
        NpcReactions.Update(FollowManager.Followers);

        if (FollowManager.HasWalkAnimals)
            GrazingMgr.Update(FollowManager.Followers);
    }

    private void OnTimeChanged(object sender, TimeChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !FollowManager.HasFollowers)
            return;

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
        GrazingMgr.Reset();
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
        GrazingMgr.Reset();
    }

    private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Config.Enabled)
            return;

        if (!e.Button.IsActionButton())
            return;

        var heldItem = Game1.player.CurrentItem;
        if (heldItem == null || heldItem.QualifiedItemId != GrazingBellItem.QualifiedItemId)
            return;

        var cursorTile = e.Cursor.GrabTile;
        var location = Game1.player.currentLocation;
        if (location == null)
            return;

        FarmAnimal clickedAnimal = null;
        var cursorWorldPos = e.Cursor.AbsolutePixels;
        foreach (var animal in location.animals.Values)
        {
            if (animal.GetBoundingBox().Contains((int)cursorWorldPos.X, (int)cursorWorldPos.Y))
            {
                clickedAnimal = animal;
                break;
            }
        }

        if (clickedAnimal == null)
            return;

        Helper.Input.Suppress(e.Button);

        if (FollowManager.IsFollowing(clickedAnimal))
        {
            var result = FollowManager.TrySendHome(clickedAnimal);
            switch (result)
            {
                case SendHomeResult.Success:
                    if (Config.ShowNotifications)
                        Game1.addHUDMessage(new HUDMessage(
                            Helper.Translation.Get("hud.walk_sent_home", new { name = clickedAnimal.displayName })));
                    break;
                case SendHomeResult.InsufficientFriendship:
                    if (Config.ShowNotifications)
                        Game1.addHUDMessage(new HUDMessage(
                            Helper.Translation.Get("hud.walk_cant_send_home", new { name = clickedAnimal.displayName })));
                    break;
            }
            return;
        }

        if (clickedAnimal.isBaby())
        {
            if (Config.ShowNotifications)
                Game1.addHUDMessage(new HUDMessage(
                    Helper.Translation.Get("hud.walk_too_young", new { name = clickedAnimal.displayName })));
            return;
        }

        if (Game1.timeOfDay >= Config.AutoDeliverTime)
        {
            if (Config.ShowNotifications)
                Game1.addHUDMessage(new HUDMessage(
                    Helper.Translation.Get("hud.walk_too_late")));
            return;
        }

        if (FollowManager.StartWalk(clickedAnimal))
        {
            if (Config.ShowNotifications)
                Game1.addHUDMessage(new HUDMessage(
                    Helper.Translation.Get("hud.walk_started", new { name = clickedAnimal.displayName })));
        }
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
