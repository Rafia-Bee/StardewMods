using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreQuestsFramework.Rendering;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;

namespace MoreQuests;

/// The "stock the shelves" beat of Story.PierreDontGetCaught. While the Stock step is live,
/// Joja's shelves act like a special-order drop box: each aisle floats the "!" bubble,
/// and pressing the action button at a shelf opens the deposit grid (the framework's reusable
/// TryOpenDepositBox) that only accepts cheap pickled rice or wheat. Outside the quest the
/// shelves do their normal thing (a flavor message), so regular Joja shopping is untouched.
internal static class JojaShelfPatches
{
    private const string JojaName = "JojaMart";

    // Joja's shelves are the only Buildings tiles whose action is a Message "JojaMart.N".
    // That cleanly separates them from the registers (JojaShop), the membership counter (JoinJoja),
    // walls, and the back office.
    private static List<Point>? _indicatorTiles;

    internal static void Apply(Harmony harmony, IModHelper helper, IMonitor monitor)
    {
        var target = AccessTools.Method(typeof(StardewValley.Locations.JojaMart), nameof(GameLocation.checkAction));
        if (target == null)
        {
            monitor.Log("Couldn't find JojaMart.checkAction; Joja shelf deposit disabled.", LogLevel.Warn);
        }
        else
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(JojaShelfPatches), nameof(CheckAction_Prefix)));
        }
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
    }

    internal static bool IsCheapPickle(Item? item)
    {
        if (item is not SObject o || o.preserve.Value != SObject.PreserveType.Pickle)
            return false;
        string src = o.preservedParentSheetIndex.Value ?? "";
        if (src.StartsWith("(O)"))
            src = src.Substring(3);
        return src == "262" || src == "271";
    }

    private static bool IsShelfAction(string? action)
        => action != null
           && action.StartsWith("Message", System.StringComparison.Ordinal)
           && action.Contains("JojaMart.");

    private static bool IsShelfTile(GameLocation location, int x, int y)
        => IsShelfAction(location.doesTileHaveProperty(x, y, "Action", "Buildings"));

    // Runs before vanilla so a shelf press opens the deposit box instead of showing the flavor
    // message. Only kicks in while the Stock step is live; otherwise vanilla runs untouched.
    private static bool CheckAction_Prefix(GameLocation __instance, xTile.Dimensions.Location tileLocation, Farmer who, ref bool __result)
    {
        if (who is null || !who.IsLocalPlayer)
            return true;
        if (ModEntry.ModScope is null || ModEntry.ModScope.GetActiveCustomSteps(ModEntry.PierreStockHandler).Count == 0)
            return true;
        if (!IsShelfTile(__instance, tileLocation.X, tileLocation.Y))
            return true;

        bool opened = ModEntry.ModScope.TryOpenDepositBox(
            ModEntry.PierreStockHandler,
            IsCheapPickle,
            rows: 3,
            title: ModEntry.I18n.Get("quest.story.pierreDontGetCaught.depositTitle").ToString());
        __result = opened;
        return !opened;
    }

    private static void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.eventUp || Game1.currentLocation?.Name != JojaName)
            return;
        if (ModEntry.ModScope is null || ModEntry.ModScope.GetActiveCustomSteps(ModEntry.PierreStockHandler).Count == 0)
            return;

        foreach (var tile in IndicatorTilesFor(Game1.currentLocation))
            DepositBoxIndicator.Draw(e.SpriteBatch, tile.X, tile.Y);
    }

    // One "!" per aisle (a run of adjacent shelf columns), placed at the aisle's front so the
    // store isn't carpeted in bubbles. Deposits still work at any shelf, this is just signage.
    private static List<Point> IndicatorTilesFor(GameLocation location)
    {
        if (_indicatorTiles != null)
            return _indicatorTiles;

        var shelves = new List<Point>();
        var layer = location.Map?.GetLayer("Buildings");
        if (layer != null)
        {
            for (int x = 0; x < layer.LayerWidth; x++)
                for (int y = 0; y < layer.LayerHeight; y++)
                    if (IsShelfTile(location, x, y))
                        shelves.Add(new Point(x, y));
        }

        var indicators = new List<Point>();
        if (shelves.Count > 0)
        {
            var columns = shelves.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
            var aisleOfColumn = new Dictionary<int, int>();
            int aisle = 0, prev = int.MinValue;
            foreach (int col in columns)
            {
                if (col - prev > 1)
                    aisle++;
                aisleOfColumn[col] = aisle;
                prev = col;
            }
            foreach (var group in shelves.GroupBy(p => aisleOfColumn[p.X]))
            {
                int frontY = group.Max(p => p.Y);
                int x = group.Where(p => p.Y == frontY).Min(p => p.X);
                indicators.Add(new Point(x, frontY));
            }
        }

        _indicatorTiles = indicators;
        return indicators;
    }
}
