using System;
using System.Collections.Generic;
using System.Linq;
using BiggerAutoGrabber.Framework;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;

namespace BiggerAutoGrabber;

public class ModEntry : Mod
{
    internal static ModConfig Config;

    private IGenericModConfigMenuApi _gmcm;

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

        Config.CapacityByType ??= new Dictionary<string, int>();
        Config.KnownTypes ??= new List<string>();

        bool changed = MigrateLegacyConfig();
        changed |= SeedSpecialTypes();
        ClampConfig();
        if (changed)
            helper.WriteConfig(Config);

        var harmony = new Harmony(ModManifest.UniqueID);
        ChestPatches.Apply(harmony);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (_, _) => StampAllStorage();
        helper.Events.GameLoop.DayStarted += (_, _) => StampAllStorage();
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.Display.MenuChanged += OnMenuChanged;
    }

    // ── Config menu ─────────────────────────────────────────────────

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        _gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_gmcm == null)
            return;

        BuildMenu();
    }

    private void BuildMenu()
    {
        _gmcm.Register(ModManifest,
            reset: () =>
            {
                var known = Config.KnownTypes;
                Config = new ModConfig { KnownTypes = known };
                SeedSpecialTypes();
            },
            save: () =>
            {
                ClampConfig();
                Helper.WriteConfig(Config);
                if (Context.IsWorldReady)
                    StampAllStorage();
            });

        _gmcm.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.title"));
        _gmcm.AddParagraph(ModManifest, () => Helper.Translation.Get("config.section.note"));

        _gmcm.AddNumberOption(ModManifest,
            () => Config.DefaultCapacity,
            v => Config.DefaultCapacity = v,
            () => Helper.Translation.Get("config.default.name"),
            () => Helper.Translation.Get("config.default.tooltip"),
            36, 516, 12);

        foreach (string key in OrderedTypes())
            AddTypeOption(key);
    }

    private void RebuildMenu()
    {
        if (_gmcm == null)
            return;

        _gmcm.Unregister(ModManifest);
        BuildMenu();
    }

    private void AddTypeOption(string key)
    {
        _gmcm.AddNumberOption(ModManifest,
            () => StorageRules.ResolveCapacity(key, Config),
            v =>
            {
                // Storing only real overrides means the default slider keeps
                // applying to any type the player hasn't deliberately changed.
                if (v == Config.DefaultCapacity)
                    Config.CapacityByType.Remove(key);
                else
                    Config.CapacityByType[key] = v;
            },
            () => DisplayNameForKey(key),
            null,
            36, 516, 12);
    }

    private IEnumerable<string> OrderedTypes()
    {
        var specials = new[] { StorageRules.AutoGrabberKey, StorageRules.FridgeKey };
        var rest = Config.KnownTypes
            .Where(k => k != StorageRules.AutoGrabberKey && k != StorageRules.FridgeKey)
            .OrderBy(DisplayNameForKey, StringComparer.OrdinalIgnoreCase);
        return specials.Concat(rest);
    }

    private string DisplayNameForKey(string key)
    {
        if (key == StorageRules.AutoGrabberKey)
            return Helper.Translation.Get("config.autograbber.name");
        if (key == StorageRules.FridgeKey)
            return Helper.Translation.Get("config.fridge.name");

        return ItemRegistry.GetData(key)?.DisplayName ?? key;
    }

    // ── Menu resize hook ────────────────────────────────────────────

    private void OnMenuChanged(object sender, MenuChangedEventArgs e)
    {
        ChestPatches.ClearScrollState();

        if (e.NewMenu is not ItemGrabMenu menu)
            return;

        var chest = ChestPatches.FindStorageChest(menu);
        if (chest == null)
            return;

        if (!chest.modData.TryGetValue(ChestPatches.CapacityKey, out string capStr)
            || !int.TryParse(capStr, out int cap))
            return;

        ChestPatches.ResizeMenu(menu, cap);
    }

    // ── Stamping ────────────────────────────────────────────────────

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        bool discovered = false;
        foreach (var pair in e.Added)
            discovered |= StampStorage(pair.Value);

        if (discovered)
            PersistAndRebuildMenu();
    }

    private void StampAllStorage()
    {
        bool discovered = false;

        Utility.ForEachLocation(location =>
        {
            foreach (var obj in location.Objects.Values)
                discovered |= StampStorage(obj);

            discovered |= StampLocationFridge(location);
            return true;
        });

        if (discovered)
            PersistAndRebuildMenu();

        Monitor.Log(
            $"Stamped all storage. {Config.KnownTypes.Count} known types, default {Config.DefaultCapacity}.",
            LogLevel.Trace);
    }

    /// <summary>Stamps one world object. Returns true if it was a storage type we hadn't seen before.</summary>
    private bool StampStorage(StardewValley.Object obj)
    {
        var kind = StorageRules.Classify(obj);
        if (kind == StorageKind.None)
            return false;

        string key = StorageRules.TypeKeyFor(kind, obj.QualifiedItemId);
        bool discovered = EnsureKnownType(key);
        ApplyStamp(StorageRules.GetBackingChest(obj, kind), key);
        return discovered;
    }

    /// <summary>Stamps a location's built-in fridge, which lives outside its object list.</summary>
    private bool StampLocationFridge(GameLocation location)
    {
        Chest fridge = location switch
        {
            FarmHouse farmHouse => farmHouse.fridge.Value,
            IslandFarmHouse islandHouse => islandHouse.fridge.Value,
            _ => null
        };

        if (fridge == null)
            return false;

        bool discovered = EnsureKnownType(StorageRules.FridgeKey);
        ApplyStamp(fridge, StorageRules.FridgeKey);
        return discovered;
    }

    private void ApplyStamp(Chest chest, string key)
    {
        if (chest == null || key == null)
            return;

        int? cap = StorageRules.ResolveStampCapacity(key, Config);
        if (cap.HasValue)
            chest.modData[ChestPatches.CapacityKey] = cap.Value.ToString();
        else
            chest.modData.Remove(ChestPatches.CapacityKey);
    }

    private bool EnsureKnownType(string key)
    {
        if (key == null || Config.KnownTypes.Contains(key))
            return false;

        Config.KnownTypes.Add(key);
        return true;
    }

    private void PersistAndRebuildMenu()
    {
        Helper.WriteConfig(Config);
        RebuildMenu();
    }

    // ── Config housekeeping ─────────────────────────────────────────

    private bool MigrateLegacyConfig()
    {
        bool changed = false;

        if (Config.Capacity.HasValue)
        {
            Config.CapacityByType[StorageRules.AutoGrabberKey] = Config.Capacity.Value;
            Config.Capacity = null;
            changed = true;
        }

        if (Config.FridgeCapacity.HasValue)
        {
            Config.CapacityByType[StorageRules.FridgeKey] = Config.FridgeCapacity.Value;
            Config.FridgeCapacity = null;
            changed = true;
        }

        if (Config.ChestCapacity.HasValue)
        {
            Config.DefaultCapacity = Config.ChestCapacity.Value;
            Config.ChestCapacity = null;
            changed = true;
        }

        return changed;
    }

    private bool SeedSpecialTypes()
    {
        bool changed = false;

        foreach (string key in new[] { StorageRules.AutoGrabberKey, StorageRules.FridgeKey })
        {
            if (!Config.KnownTypes.Contains(key))
            {
                Config.KnownTypes.Add(key);
                changed = true;
            }
        }

        return changed;
    }

    private void ClampConfig()
    {
        if (Config.DefaultCapacity < 36)
            Config.DefaultCapacity = 36;

        foreach (string key in Config.CapacityByType.Keys.ToList())
        {
            if (Config.CapacityByType[key] < 36)
                Config.CapacityByType[key] = 36;
        }
    }
}
