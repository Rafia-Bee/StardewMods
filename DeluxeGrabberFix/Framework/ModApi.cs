using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Interfaces;
using StardewModdingAPI;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

public class ModApi : IDeluxeGrabberFixApi
{
    private readonly Mod Mod;
    private Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> _getMushroomHarvest;
    private Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> _getBerryBushHarvest;
    private Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> _getFruitTreeHarvest;
    private Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> _getSlimeHarvest;

    public event Action<Item, GameLocation> OnItemGrabbed;

    public Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetMushroomHarvest
    {
        get => _getMushroomHarvest;
        set
        {
            if (_getMushroomHarvest != null && _getMushroomHarvest != value)
                Mod.Monitor.LogOnce("GetMushroomHarvest override is being set more than once. This usually means that multiple mods are conflicting when attempting to integrate with DeluxeGrabberFix.", LogLevel.Warn);
            _getMushroomHarvest = value;
        }
    }

    public Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetBerryBushHarvest
    {
        get => _getBerryBushHarvest;
        set
        {
            if (_getBerryBushHarvest != null && _getBerryBushHarvest != value)
                Mod.Monitor.LogOnce("GetBerryBushHarvest override is being set more than once. This usually means that multiple mods are conflicting when attempting to integrate with DeluxeGrabberFix.", LogLevel.Warn);
            _getBerryBushHarvest = value;
        }
    }

    public Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetFruitTreeHarvest
    {
        get => _getFruitTreeHarvest;
        set
        {
            if (_getFruitTreeHarvest != null && _getFruitTreeHarvest != value)
                Mod.Monitor.LogOnce("GetFruitTreeHarvest override is being set more than once. This usually means that multiple mods are conflicting when attempting to integrate with DeluxeGrabberFix.", LogLevel.Warn);
            _getFruitTreeHarvest = value;
        }
    }

    public Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetSlimeHarvest
    {
        get => _getSlimeHarvest;
        set
        {
            if (_getSlimeHarvest != null && _getSlimeHarvest != value)
                Mod.Monitor.LogOnce("GetSlimeHarvest override is being set more than once. This usually means that multiple mods are conflicting when attempting to integrate with DeluxeGrabberFix.", LogLevel.Warn);
            _getSlimeHarvest = value;
        }
    }

    public bool IsGrabberActive(GameLocation location)
    {
        if (location == null)
            return false;

        return location.Objects.Pairs.Any(pair =>
            pair.Value.QualifiedItemId == BigCraftableIds.AutoGrabber
            && pair.Value.heldObject.Value is StardewValley.Objects.Chest);
    }

    internal void RaiseOnItemGrabbed(Item item, GameLocation location)
    {
        OnItemGrabbed?.Invoke(item, location);
    }

    public ModApi(Mod mod)
    {
        Mod = mod;
    }
}
