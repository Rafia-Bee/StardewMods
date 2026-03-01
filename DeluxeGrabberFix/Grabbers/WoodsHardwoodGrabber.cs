using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Locations;
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

    public override bool GrabItems()
    {
        if (!Config.fellSecretWoodsStumps)
            return false;

        if (Location is not Woods woods || woods.resourceClumps.Count == 0)
            return false;

        Tool axe = Player.getToolFromName("Axe");
        bool result = false;

        for (int i = woods.resourceClumps.Count - 1; i >= 0; i--)
        {
            ResourceClump clump = woods.resourceClumps[i];
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

            if (Player.professions.Contains(12) && random.NextDouble() < 0.5)
                hardwoodCount++;

            items.Add(ItemRegistry.Create<Object>(709.ToString(), hardwoodCount));

            if (random.NextDouble() < 0.1)
                items.Add(ItemRegistry.Create<Object>(292.ToString()));

            if (Game1.random.NextDouble() <= 0.25 && Player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
                items.Add(ItemRegistry.Create<Object>(890.ToString()));

            if (TryAddItems((IEnumerable<Item>)items))
            {
                woods.resourceClumps.RemoveAt(i);
                result = true;
            }
        }
        return result;
    }
}
