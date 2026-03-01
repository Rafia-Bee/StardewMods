using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.SpecialOrders;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class SeedTreeGrabber : TerrainFeaturesMapGrabber
{
    public SeedTreeGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.seedTrees)
            return false;

        if (feature is not Tree tree || !IsHarvestableSeedTree(tree))
            return false;

        var items = new List<Item>();
        int seedId = tree.treeType.Value switch
        {
            "3" => 311,
            "1" => 309,
            "8" => 292,
            "2" => 310,
            "6" or "9" => 88,
            _ => -1
        };

        if (Game1.GetSeasonForLocation(Location).Equals("fall")
            && tree.treeType.Value == "2"
            && Game1.dayOfMonth >= 14)
        {
            seedId = 408;
        }

        if (seedId != -1)
            items.Add(ItemRegistry.Create<Object>(seedId.ToString()));

        if (seedId == 88 && new Random((int)Game1.uniqueIDForThisGame + (int)Game1.stats.DaysPlayed + (int)tile.X * 13 + (int)tile.Y * 54).NextDouble() < 0.1)
        {
            items.Add(ItemRegistry.Create<Object>(791.ToString()));
        }

        if (Game1.random.NextDouble() <= 0.5 && Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
        {
            items.Add(ItemRegistry.Create<Object>(890.ToString()));
        }

        if (TryAddItems(items))
        {
            tree.hasSeed.Value = false;
            return true;
        }
        return false;
    }

    private bool IsHarvestableSeedTree(Tree tree)
    {
        if (tree.growthStage.Value < 5 || tree.stump.Value || !tree.hasSeed.Value)
            return false;

        return Game1.IsMultiplayer || Player.ForagingLevel >= 1;
    }
}
