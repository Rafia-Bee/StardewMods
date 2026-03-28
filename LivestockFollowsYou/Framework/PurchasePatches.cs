using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace LivestockFollowsYou.Framework;

/// <summary>Harmony patches for intercepting animal purchase and suppressing vanilla behaviors during follow.</summary>
internal static class PurchasePatches
{
    internal static AnimalFollowManager Manager;
    internal static Func<ModConfig> GetConfig;
    internal static IMonitor Monitor;

    /// <summary>Register all Harmony patches.</summary>
    public static void Apply(Harmony harmony)
    {
        // Intercept animal adoption — fires for vanilla PurchaseAnimalsMenu, Livestock Bazaar, and any other mod
        harmony.Patch(
            original: AccessTools.Method(typeof(AnimalHouse), nameof(AnimalHouse.adoptAnimal)),
            postfix: new HarmonyMethod(typeof(PurchasePatches), nameof(AdoptAnimal_Postfix))
        );

        // Skip vanilla behaviors (grass-seeking, return-to-barn pathfinding) for following animals
        harmony.Patch(
            original: AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.behaviors)),
            prefix: new HarmonyMethod(typeof(PurchasePatches), nameof(Behaviors_Prefix))
        );

        // Prevent sleep halt during follow
        harmony.Patch(
            original: AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.SleepIfNecessary)),
            prefix: new HarmonyMethod(typeof(PurchasePatches), nameof(SleepIfNecessary_Prefix))
        );

        // Prevent random direction changes during follow
        harmony.Patch(
            original: AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.UpdateRandomMovements)),
            prefix: new HarmonyMethod(typeof(PurchasePatches), nameof(UpdateRandomMovements_Prefix))
        );
    }

    private static void AdoptAnimal_Postfix(AnimalHouse __instance, FarmAnimal animal)
    {
        if (!GetConfig().Enabled)
            return;

        // Only intercept newly purchased animals, not animals being moved between buildings
        if (animal.daysOwned.Value > 0)
            return;

        if (Manager.IsFollowing(animal))
            return;

        Manager.StartFollowing(animal);
    }

    private static bool Behaviors_Prefix(FarmAnimal __instance, ref bool __result)
    {
        if (Manager?.IsFollowing(__instance) != true)
            return true;

        __result = false;
        return false;
    }

    private static bool SleepIfNecessary_Prefix(FarmAnimal __instance, ref bool __result)
    {
        if (Manager?.IsFollowing(__instance) != true)
            return true;

        __result = false;
        return false;
    }

    private static bool UpdateRandomMovements_Prefix(FarmAnimal __instance)
    {
        if (Manager?.IsFollowing(__instance) != true)
            return true;

        return false;
    }
}
