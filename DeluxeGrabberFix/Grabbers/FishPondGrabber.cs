using StardewValley;
using StardewValley.Buildings;

namespace DeluxeGrabberFix.Grabbers;

internal class FishPondGrabber : MapGrabber
{
    public FishPondGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabItems()
    {
        if (Config.disableMachineCollection || !Config.collectFishPonds)
            return false;

        bool grabbed = false;

        foreach (var building in Location.buildings)
        {
            if (building is not FishPond pond)
                continue;

            if (pond.output.Value == null)
                continue;

            var item = pond.output.Value;
            if (TryAddItem(item))
            {
                Mod.LogDebug($"Collected {item.Name} x{item.Stack} from fish pond at {Location.Name}");
                pond.output.Value = null;

                if (item is Object obj)
                {
                    int xp = (int)(obj.sellToStorePrice(-1L) * FishPond.HARVEST_OUTPUT_EXP_MULTIPLIER)
                             + FishPond.HARVEST_BASE_EXP;
                    GainExperience(1, xp);
                }

                grabbed = true;
            }
        }

        return grabbed;
    }
}
