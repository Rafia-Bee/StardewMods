using System;
using System.Collections.Generic;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

/// <summary>
/// Intercepts items created via Game1.createItemDebris during a controlled harvest cycle,
/// redirecting them into a collection list instead of spawning ground debris.
/// </summary>
internal static class HarvestInterceptor
{
    private static bool _intercepting;
    private static List<Item> _interceptedItems;

    public static bool IsIntercepting => _intercepting;

    // Audit §4.6: SMAPI is single-threaded, so reentry can only happen if a third-party
    // Harmony patch on a method DGF calls during a grab cycle (Crop.harvest, Tree.shake,
    // etc.) calls back into another DGF grabber. The previous behavior silently overwrote
    // _interceptedItems and lost the outer frame's items. Throwing here surfaces the
    // conflict in SMAPI's log so the user can identify the offending mod, instead of
    // failing as silent item loss that looks like a DGF bug.
    public static void BeginIntercept()
    {
        if (_intercepting)
            throw new InvalidOperationException(
                "HarvestInterceptor.BeginIntercept was called while a previous intercept is still active. "
                + "This indicates a reentrant harvest cycle (likely caused by a third-party Harmony patch "
                + "on a vanilla harvest method calling back into a DGF grabber). Aborting before silent item loss.");
        _interceptedItems = new List<Item>();
        _intercepting = true;
    }

    public static List<Item> EndIntercept()
    {
        _intercepting = false;
        var items = _interceptedItems;
        _interceptedItems = null;
        return items ?? new List<Item>();
    }

    // Belt-and-suspenders cleanup. Every grabber call site already wraps Begin/End in
    // try/finally, but if a future caller forgets, GrabSession.Dispose calls this so a
    // leaked _intercepting flag never survives past one grab cycle. Without this, a
    // single uncaught exception inside an intercepted harvest left _intercepting=true
    // forever, and the next BeginIntercept threw the audit 4.6 reentry check, which
    // bricked every subsequent grab in the session.
    internal static void ForceReset()
    {
        _intercepting = false;
        _interceptedItems = null;
    }

    /// <summary>
    /// Harmony prefix for Game1.createItemDebris. When intercepting, captures items
    /// instead of spawning debris on the ground.
    /// </summary>
    internal static bool CreateItemDebris_Prefix(Item item)
    {
        if (!_intercepting || _interceptedItems == null)
            return true;

        _interceptedItems.Add(item);
        return false;
    }

    /// Harmony prefix for the terminal Game1.createObjectDebris(string id, int xTile,
    /// int yTile, int groundLevel, int itemQuality, float velocityMultiplyer, GameLocation
    /// location) overload. Tree.shake routes the Qi Bean DROP_QI_BEANS drop and a few
    /// other rare drops through this path (issue #75.1) instead of through
    /// createItemDebris(Item, ...), so without this prefix those items spawn on the
    /// ground even mid-intercept. Materializes an Item from the id and routes it through
    /// the same interception list as createItemDebris.
    internal static bool CreateObjectDebris_Prefix(string id, int itemQuality)
    {
        if (!_intercepting || _interceptedItems == null)
            return true;

        var item = ItemRegistry.Create(id);
        if (item == null)
            return true;

        if (itemQuality > 0 && item is Object obj)
            obj.Quality = itemQuality;

        _interceptedItems.Add(item);
        return false;
    }
}
