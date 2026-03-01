using HarmonyLib;
using StardewValley.Objects;

namespace BiggerAutoGrabber.Framework;

internal static class ChestPatches
{
    internal const string CapacityKey = "BiggerAutoGrabber/Capacity";

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(Chest), nameof(Chest.GetActualCapacity)),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(GetActualCapacity_Postfix))
        );
    }

    private static void GetActualCapacity_Postfix(Chest __instance, ref int __result)
    {
        if (__instance.modData.TryGetValue(CapacityKey, out string val)
            && int.TryParse(val, out int cap)
            && cap > 0)
        {
            __result = cap;
        }
    }
}
