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

    public static void BeginIntercept()
    {
        _interceptedItems = new List<Item>();
        _intercepting = true;
    }

    public static List<Item> EndIntercept()
    {
        _intercepting = false;
        var items = _interceptedItems;
        _interceptedItems = null;
        return items;
    }

    /// <summary>
    /// Harmony prefix for Game1.createItemDebris. When intercepting, captures items
    /// instead of spawning debris on the ground.
    /// </summary>
    internal static bool CreateItemDebris_Prefix(Item item)
    {
        if (!_intercepting)
            return true;

        _interceptedItems?.Add(item);
        return false;
    }
}
