using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Interfaces;

public interface IAutomateAPI
{
    IDictionary<Vector2, int> GetMachineStates(GameLocation location, Rectangle tileArea);
}
