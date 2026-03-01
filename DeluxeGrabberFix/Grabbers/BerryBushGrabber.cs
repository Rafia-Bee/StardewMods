using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
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
        if (berry.ParentSheetIndex == 815)
            berry.Quality = 0;
        else
            berry.Quality = Player.professions.Contains(16) ? 4 : 0;

        return new KeyValuePair<Object, int>(berry, 0);
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.bushes || !IsForageableBush(feature, out var bush))
            return false;

        var items = new List<Object>();
        string shakeOffItem = bush.GetShakeOffItem();
        int exp = 0;
        Random random = new((int)tile.X + (int)tile.Y * 5000 + (int)Game1.uniqueIDForThisGame + (int)Game1.stats.DaysPlayed);

        if (shakeOffItem != "-1" && shakeOffItem != "(O)73")
        {
            if (bush.size.Value == 3 || bush.size.Value == 4)
            {
                var harvest = GetBerryBushHarvest(ItemRegistry.Create<Object>(shakeOffItem), tile, Location);
                items.Add(harvest.Key);
                exp = harvest.Value;
            }
            else
            {
                int count = random.Next(1, 2) + Player.ForagingLevel / 4;
                for (int i = 0; i < count; i++)
                {
                    var harvest = GetBerryBushHarvest(ItemRegistry.Create<Object>(shakeOffItem), tile, Location);
                    items.Add(harvest.Key);
                    if (i == 0)
                        exp = harvest.Value;
                }
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
