using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

// Soft-detected compatibility shim for Spiderbuttons.Agromancy. Agromancy's
// harvest postfix grants extra crop yield and bonus seeds by calling its own
// CropManager.createObjectDebrisWithEssence, which builds a Debris directly
// and pushes it into location.debris. That path bypasses Game1.createItemDebris
// and Game1.createObjectDebris, which are the two methods DGF's HarvestInterceptor
// Harmony-patches. So during a DGF grab cycle the base yield gets captured into
// the auto-grabber, but every extra grape (or extra seed) from a yield-boosted
// crop falls on the ground next to the trellis. This integration patches
// Agromancy's helper so that when an intercept is active, the extra item is
// added to the interceptor list instead of spawning ground debris.
internal static class AgromancyIntegration
{
    private const string AgromancyModId = "Spiderbuttons.Agromancy";

    private static MethodInfo _applyEssences;

    public static bool Loaded { get; private set; }

    public static void Initialize(IModRegistry registry, Harmony harmony, ModEntry mod)
    {
        if (!registry.IsLoaded(AgromancyModId))
            return;

        try
        {
            Type cropManagerType = ResolveType("Agromancy.CropManager");
            Type cropEssencesType = ResolveType("Agromancy.Models.CropEssences");
            Type essenceCalculatorType = ResolveType("Agromancy.EssenceCalculator");

            if (cropManagerType == null || cropEssencesType == null)
            {
                mod.LogDebug("Agromancy detected but types could not be resolved; extra-yield interception disabled.");
                return;
            }

            MethodInfo target = cropManagerType.GetMethod(
                "createObjectDebrisWithEssence",
                BindingFlags.Public | BindingFlags.Static);

            if (target == null)
            {
                mod.LogDebug("Agromancy detected but CropManager.createObjectDebrisWithEssence could not be resolved; extra-yield interception disabled.");
                return;
            }

            _applyEssences = essenceCalculatorType?.GetMethod(
                "ApplyEssences",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(IHaveModData), cropEssencesType },
                modifiers: null);

            harmony.Patch(
                original: target,
                prefix: new HarmonyMethod(typeof(AgromancyIntegration), nameof(CreateObjectDebrisWithEssence_Prefix)));

            Loaded = true;
            mod.LogDebug("Agromancy detected -- extra-yield interception enabled.");
        }
        catch (Exception ex)
        {
            mod.LogDebug($"Agromancy integration setup failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Type ResolveType(string fullName)
    {
        Type type = Type.GetType(fullName + ", Agromancy", throwOnError: false);
        if (type != null)
            return type;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Agromancy")
                continue;
            type = asm.GetType(fullName);
            if (type != null)
                return type;
        }
        return null;
    }

    // Prefix on Agromancy.CropManager.createObjectDebrisWithEssence. Signature
    // (from Agromancy 1.0.3): (string id, int xTile, int yTile, CropEssences essences,
    //  int groundLevel = -1, int itemQuality = 0, float velocityMultiplyer = 1f,
    //  GameLocation? location = null). We only consume the params we need; Harmony
    // matches the rest by position from the original method.
    internal static bool CreateObjectDebrisWithEssence_Prefix(string id, object essences, int itemQuality)
    {
        if (!HarvestInterceptor.IsIntercepting)
            return true;

        Item item = ItemRegistry.Create(id, quality: itemQuality);
        if (item == null)
            return true;

        if (_applyEssences != null && essences != null)
        {
            try
            {
                _applyEssences.Invoke(null, new[] { (object)item, essences });
            }
            catch
            {
                // If applying essences blows up for any reason, still grab the plain
                // item rather than dropping it. The visual essence sparkle is
                // cosmetic; the player's main concern is not losing the yield.
            }
        }

        return !HarvestInterceptor.TryInterceptItem(item);
    }
}
