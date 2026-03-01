using System;
using System.Linq;
using BiggerAutoGrabber.Framework;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace BiggerAutoGrabber;

public class ModEntry : Mod
{
    internal static ModConfig Config;

    public override void Entry(IModHelper helper)
    {
        try
        {
            Config = helper.ReadConfig<ModConfig>();
        }
        catch (Exception)
        {
            Config = new ModConfig();
        }

        if (Config.Capacity < 12)
            Config.Capacity = 12;

        var harmony = new Harmony(ModManifest.UniqueID);
        ChestPatches.Apply(harmony);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (_, _) => StampAllAutoGrabbers();
        helper.Events.GameLoop.DayStarted += (_, _) => StampAllAutoGrabbers();
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
    }

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(ModManifest,
            () => Config = new ModConfig(),
            () =>
            {
                Helper.WriteConfig(Config);
                if (Context.IsWorldReady)
                    StampAllAutoGrabbers();
            });

        api.AddNumberOption(ModManifest,
            () => Config.Capacity,
            v => Config.Capacity = v,
            () => "Auto-Grabber Capacity",
            () => "Number of item slots in each auto-grabber. Vanilla default is 36.",
            36, 288, 12);
    }

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        foreach (var pair in e.Added)
        {
            StampIfAutoGrabber(pair.Value);
        }
    }

    private void StampAllAutoGrabbers()
    {
        var allLocations = Game1.locations
            .Concat(Game1.getFarm().buildings.Select(b => b.indoors.Value))
            .Where(loc => loc != null);

        foreach (var location in allLocations)
        {
            foreach (var obj in location.Objects.Values)
            {
                StampIfAutoGrabber(obj);
            }
        }

        Monitor.Log($"Stamped all auto-grabbers with capacity {Config.Capacity}.", LogLevel.Trace);
    }

    private void StampIfAutoGrabber(StardewValley.Object obj)
    {
        if (obj != null
            && obj.bigCraftable.Value
            && obj.ParentSheetIndex == 165
            && obj.heldObject.Value is Chest chest)
        {
            chest.modData[ChestPatches.CapacityKey] = Config.Capacity.ToString();
        }
    }
}
