using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal static class MachineOutputPatch
{
    internal static void MinutesElapsed_Prefix(Object __instance, ref bool __state)
    {
        __state = __instance.readyForHarvest.Value;
    }

    internal static void MinutesElapsed_Postfix(Object __instance, bool __state)
    {
        if (!__state && __instance.readyForHarvest.Value && __instance.heldObject.Value != null)
        {
            ModEntry.FlagMachineReadyLocation(__instance.Location);
        }
    }
}
