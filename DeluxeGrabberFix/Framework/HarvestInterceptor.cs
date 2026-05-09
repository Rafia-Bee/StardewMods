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
}
