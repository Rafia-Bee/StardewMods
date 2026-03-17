using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.SpecialOrders;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace DeluxeGrabberFix.Grabbers;

internal class WoodsHardwoodGrabber : MapGrabber
{
    public WoodsHardwoodGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    private static bool IsHardwoodClump(ResourceClump clump)
    {
        if (clump.modData.ContainsKey("spacechase0.SpaceCore/LargeMinable"))
            return false;

        return clump.parentSheetIndex.Value == ResourceClump.stumpIndex
            || clump.parentSheetIndex.Value == ResourceClump.hollowLogIndex;
    }

    public override bool GrabItems()
    {
        if (!Config.fellHardwoodStumps)
            return false;

        if (Location.resourceClumps.Count == 0)
            return false;

        Tool axe = Player.getToolFromName("Axe");
        bool result = false;

        for (int i = Location.resourceClumps.Count - 1; i >= 0; i--)
        {
            ResourceClump clump = Location.resourceClumps[i];

            if (!IsHardwoodClump(clump))
                continue;
            Vector2 tile = clump.Tile;
            var items = new List<Object>();

            if (Location.HasUnlockedAreaSecretNotes(Player) && Game1.random.NextDouble() < 0.05)
            {
                Object secretNote = Location.tryToCreateUnseenSecretNote(Player);
                if (secretNote != null)
                    items.Add(secretNote);
            }

            Random random = new((int)Game1.uniqueIDForThisGame + (int)Game1.stats.DaysPlayed + (int)tile.X * 7 + (int)tile.Y * 11);
            int hardwoodCount = 2;

            if (axe != null)
            {
                float damagePerHit = Math.Max(1f, (axe.UpgradeLevel + 1) * 0.75f);
                bool hasShaving = axe is Axe && axe.hasEnchantmentOfType<ShavingEnchantment>() && Game1.random.NextDouble() <= damagePerHit / 12f;
                int hitCount = (int)Math.Ceiling(clump.health.Value / damagePerHit);
                if (hasShaving)
                    hardwoodCount += hitCount;
            }

            if (Player.professions.Contains(Framework.ProfessionIds.Lumberjack) && random.NextDouble() < 0.5)
                hardwoodCount++;

            items.Add(ItemRegistry.Create<Object>(ItemIds.Hardwood, hardwoodCount));

            if (random.NextDouble() < 0.1)
                items.Add(ItemRegistry.Create<Object>(ItemIds.MahogonySeed));

            if (Game1.random.NextDouble() <= 0.25 && Player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
                items.Add(ItemRegistry.Create<Object>(ItemIds.QiBean));

            if (TryAddItems((IEnumerable<Item>)items))
            {
                Location.resourceClumps.RemoveAt(i);
                result = true;
            }
        }
        return result;
    }
}
