using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

// Shared harvest for a ready bush (berry, tea, or a Custom Bush plant). Used both
// for bushes growing in the ground and for bushes planted in garden pots (issue
// #115), so a potted tea bush yields the same item and quality as one in the
// ground. The caller stores the items and then resets the bush.
internal static class BushHarvest
{
    internal static bool TryGetHarvest(ModEntry mod, Bush bush, Vector2 tile, GameLocation location, out List<Item> items, out int exp)
    {
        items = new List<Item>();
        exp = 0;

        if (bush == null || bush.townBush.Value || bush.tileSheetOffset.Value != 1 || !bush.inBloom())
            return false;

        var customBushApi = mod.CustomBushApi;
        if (customBushApi != null && customBushApi.IsCustomBush(bush))
        {
            if (!customBushApi.TryGetShakeOffItem(bush, out var customItem) || customItem == null)
                return false;
            items.Add(customItem);
            return true;
        }

        string shakeOffItem = bush.GetShakeOffItem();
        if (string.IsNullOrEmpty(shakeOffItem) || shakeOffItem == "-1" || shakeOffItem == ItemIds.GoldenWalnut)
            return false;

        var getHarvest = mod.Api.GetBerryBushHarvest ?? DefaultGetBerryBushHarvest;

        // Tea bushes (size 3) and walnut bushes (size 4) shake off a single item.
        if (bush.size.Value == 3 || bush.size.Value == 4)
        {
            var berry = ItemRegistry.Create<Object>(shakeOffItem);
            if (berry == null)
                return false;
            var harvest = getHarvest(berry, tile, location);
            items.Add(harvest.Key);
            exp = harvest.Value;
            return true;
        }

        int count = new Random((int)tile.X + (int)tile.Y * 5000 + (int)Game1.uniqueIDForThisGame + (int)Game1.stats.DaysPlayed)
            .Next(1, 2) + Game1.MasterPlayer.ForagingLevel / 4;
        for (int i = 0; i < count; i++)
        {
            var berry = ItemRegistry.Create<Object>(shakeOffItem);
            if (berry == null)
                return false;
            var harvest = getHarvest(berry, tile, location);
            items.Add(harvest.Key);
            if (i == 0)
                exp = harvest.Value;
        }
        return items.Count > 0;
    }

    // Tea leaves always come out base quality; berries get iridium for Botanists.
    private static KeyValuePair<Object, int> DefaultGetBerryBushHarvest(Object berry, Vector2 bushTile, GameLocation location)
    {
        berry.Quality = berry.QualifiedItemId == ItemIds.TeaLeaves
            ? 0
            : Game1.MasterPlayer.professions.Contains(ProfessionIds.Botanist) ? 4 : 0;
        return new KeyValuePair<Object, int>(berry, 0);
    }
}
