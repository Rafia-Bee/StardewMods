using System;
using System.Collections.Generic;
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

    public ModApi(Mod mod)
    {
        Mod = mod;
    }
}
