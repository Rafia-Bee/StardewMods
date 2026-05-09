using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Framework;

/// <summary>
/// Computes the set of tiles whose machines are managed by Automate and should be
/// skipped by DGF when automateCompatibility is enabled. Cached per (location, tick)
/// so repeat calls within the same grab cycle don't re-scan.
/// </summary>
internal static class AutomateSkipTiles
{
    private const string StoreItemsKey = "Pathoschild.Automate/StoreItems";
    private const string SuperHopperKey = "spacechase0.SuperHopper";

    private static GameLocation _cachedLocation;
    private static int _cachedTick;
    private static HashSet<Vector2> _cachedSkipTiles;

    // Memoize cleaned-name results so a BFS over a network with N "Crab Pot"s only
    // pays the LINQ + ToArray + string-ctor cost once. Capped only by the count of
    // distinct Object.Name values across all machines a player has placed; vanilla
    // is ~50, modded modlists rarely exceed a few hundred. Audit §3.6.
    private static readonly Dictionary<string, string> _cleanedNameCache = new();

    /// <summary>
    /// Returns the set of tiles Automate is managing for the given location, or null
    /// if Automate isn't installed, compat is off, or there's nothing to skip.
    /// </summary>
    public static HashSet<Vector2> Get(ModEntry mod, GameLocation location)
    {
        if (!mod.Config.Machines.automateCompatibility)
            return null;

        if (_cachedLocation == location && _cachedTick == Game1.ticks)
            return _cachedSkipTiles;

        var skipTiles = Build(mod, location);
        _cachedLocation = location;
        _cachedTick = Game1.ticks;
        _cachedSkipTiles = skipTiles;
        return skipTiles;
    }

    private static HashSet<Vector2> Build(ModEntry mod, GameLocation location)
    {
        var allMachineTiles = mod.GetAutomatedMachineStates(location);
        if (allMachineTiles == null || allMachineTiles.Count == 0)
            return null;

        var automateDisabledTypes = mod.GetAutomateDisabledMachineTypes();

        var skipTiles = new HashSet<Vector2>();
        var visited = new HashSet<Vector2>();

        foreach (var startTile in allMachineTiles.Keys)
        {
            if (visited.Contains(startTile))
                continue;

            // BFS to find the connected component (machines + connectors + chests)
            var component = new List<Vector2>();
            var hasOutputChest = false;
            var queue = new Queue<Vector2>();
            queue.Enqueue(startTile);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                    continue;

                if (allMachineTiles.ContainsKey(current)
                    && !IsAutomateDisabled(automateDisabledTypes, location, current))
                {
                    component.Add(current);
                }

                if (!hasOutputChest && location.Objects.TryGetValue(current, out var currentObj) && currentObj is Chest chest
                    && !chest.modData.ContainsKey(SuperHopperKey))
                {
                    // "Disable" means "Never put items in this chest" -- treat as input-only
                    if (!chest.modData.TryGetValue(StoreItemsKey, out var storeValue) || storeValue != "Disable")
                        hasOutputChest = true;
                }

                Vector2[] neighbors =
                {
                    new(current.X, current.Y - 1),
                    new(current.X, current.Y + 1),
                    new(current.X - 1, current.Y),
                    new(current.X + 1, current.Y)
                };

                foreach (var neighbor in neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    if (allMachineTiles.ContainsKey(neighbor))
                        queue.Enqueue(neighbor);
                    else if (location.Objects.TryGetValue(neighbor, out var nObj) && nObj is Chest nChest
                             && !nChest.modData.ContainsKey(SuperHopperKey))
                        queue.Enqueue(neighbor);
                    else if (location.terrainFeatures.TryGetValue(neighbor, out var feature) && feature is Flooring)
                        queue.Enqueue(neighbor);
                }
            }

            if (hasOutputChest)
            {
                foreach (var tile in component)
                    skipTiles.Add(tile);
            }
        }

        return skipTiles.Count > 0 ? skipTiles : null;
    }

    private static bool IsAutomateDisabled(HashSet<string> automateDisabledTypes, GameLocation location, Vector2 tile)
    {
        if (automateDisabledTypes == null)
            return false;
        if (!location.Objects.TryGetValue(tile, out var obj) || obj.Name == null)
            return false;

        return automateDisabledTypes.Contains(GetCleanedTypeId(obj.Name));
    }

    /// <summary>
    /// Returns the alphanumeric-only form of <paramref name="name"/>, allocating the
    /// cleaned string at most once per distinct input. Returns the source string
    /// directly when no characters need stripping (no allocation in the common-clean case).
    /// </summary>
    internal static string GetCleanedTypeId(string name)
    {
        if (_cleanedNameCache.TryGetValue(name, out var cached))
            return cached;

        bool clean = true;
        for (int i = 0; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]))
            {
                clean = false;
                break;
            }
        }

        string result;
        if (clean)
        {
            result = name;
        }
        else
        {
            var buffer = new char[name.Length];
            int written = 0;
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsLetterOrDigit(name[i]))
                    buffer[written++] = name[i];
            }
            result = new string(buffer, 0, written);
        }

        _cleanedNameCache[name] = result;
        return result;
    }
}
