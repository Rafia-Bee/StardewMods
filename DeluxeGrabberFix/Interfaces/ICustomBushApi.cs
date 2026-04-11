using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Interfaces;

public interface ICustomBushApi
{
    bool IsCustomBush(Bush bush);
    bool TryGetShakeOffItem(Bush bush, out Item item);
}
