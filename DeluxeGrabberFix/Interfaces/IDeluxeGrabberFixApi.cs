using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Interfaces;

public interface IDeluxeGrabberFixApi
{
    Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetMushroomHarvest { get; set; }
    Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetBerryBushHarvest { get; set; }
    Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetFruitTreeHarvest { get; set; }
    Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetSlimeHarvest { get; set; }
    event Action<Item, GameLocation> OnItemGrabbed;
    bool IsGrabberActive(GameLocation location);
}
