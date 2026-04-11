using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class BerryBushGrabber : TerrainFeaturesMapGrabber
{
    private readonly Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetBerryBushHarvest;

    public BerryBushGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        GetBerryBushHarvest = Mod.Api.GetBerryBushHarvest ?? DefaultGetBerryBushHarvest;
    }

    private KeyValuePair<Object, int> DefaultGetBerryBushHarvest(Object berry, Vector2 bushTile, GameLocation location)
    {
        if (berry.QualifiedItemId == ItemIds.TeaLeaves)
            berry.Quality = 0;
        else
            berry.Quality = Player.professions.Contains(Framework.ProfessionIds.Botanist) ? 4 : 0;

        return new KeyValuePair<Object, int>(berry, 0);
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.bushes || !IsForageableBush(feature, out var bush))
            return false;

        // Custom Bush: use its API for correct quality/quantity
        if (Mod.CustomBushApi != null && Mod.CustomBushApi.IsCustomBush(bush))
        {
            if (!Mod.CustomBushApi.TryGetShakeOffItem(bush, out var customItem) || customItem == null)
                return false;

            if (TryAddItem(customItem))
            {
                bush.tileSheetOffset.Value = 0;
                bush.setUpSourceRect();
                return true;
            }
            return false;
        }

        string shakeOffItem = bush.GetShakeOffItem();
        if (string.IsNullOrEmpty(shakeOffItem) || shakeOffItem == "-1" || shakeOffItem == ItemIds.GoldenWalnut)
            return false;

        var items = new List<Object>();
        int exp = 0;
        Random random = new((int)tile.X + (int)tile.Y * 5000 + (int)Game1.uniqueIDForThisGame + (int)Game1.stats.DaysPlayed);

        if (bush.size.Value == 3 || bush.size.Value == 4)
        {
            var berry = ItemRegistry.Create<Object>(shakeOffItem);
            if (berry == null)
                return false;
            var harvest = GetBerryBushHarvest(berry, tile, Location);
            items.Add(harvest.Key);
            exp = harvest.Value;
        }
        else
        {
            int count = random.Next(1, 2) + Player.ForagingLevel / 4;
            for (int i = 0; i < count; i++)
            {
                var berry = ItemRegistry.Create<Object>(shakeOffItem);
                if (berry == null)
                    return false;
                var harvest = GetBerryBushHarvest(berry, tile, Location);
                items.Add(harvest.Key);
                if (i == 0)
                    exp = harvest.Value;
            }
        }

        if (TryAddItems((IEnumerable<Item>)items))
        {
            bush.tileSheetOffset.Value = 0;
            bush.setUpSourceRect();
            GainExperience(2, exp);
            return true;
        }
        return false;
    }

    private bool IsForageableBush(TerrainFeature feature, out Bush bush)
    {
        bush = null;
        if (feature is Bush b)
        {
            bush = b;
            return !bush.townBush.Value
                && bush.tileSheetOffset.Value == 1
                && bush.inBloom();
        }
        return false;
    }
}
