using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Enchantments;
using StardewValley.Extensions;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Tools;

namespace DeluxeGrabberFix.Grabbers;

internal class ArtifactSpotsGrabber : ObjectsMapGrabber
{
    public ArtifactSpotsGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.artifactSpots || obj.ParentSheetIndex != 590)
            return false;

        var items = GetForagedArtifactsFromArtifactSpot(Location, tile);
        if (items != null && TryAddItems((IEnumerable<Item>)items))
        {
            Location.Objects.Remove(tile);
            return true;
        }
        return false;
    }

    private List<Object> GetForagedArtifactsFromArtifactSpot(GameLocation location, Vector2 tile)
    {
        var list = new List<Object>();
        Random random = Utility.CreateDaySaveRandom(
            tile.X * 2000f,
            tile.Y,
            Game1.netWorldState.Value.TreasureTotemsUsed * 777);

        Farmer player = Game1.player;
        bool hasGenerousEnchantment = player.CurrentTool is Hoe hoe && hoe.hasEnchantmentOfType<GenerousEnchantment>();

        var locationsData = DataLoader.Locations(Game1.content);
        LocationData data = location.GetData();
        ItemQueryContext context = new(location, player, random, "DeluxeGrabberFix.ArtifactSpot");

        IEnumerable<ArtifactSpotDropData> drops = locationsData["Default"].ArtifactSpots;
        if (data?.ArtifactSpots is { Count: > 0 })
        {
            drops = drops.Concat(data.ArtifactSpots);
        }
        drops = drops.OrderBy(p => p.Precedence);

        if (Game1.player.mailReceived.Contains("sawQiPlane")
            && random.NextDouble() < 0.05 + Game1.player.team.AverageDailyLuck() / 2.0)
        {
            list.Add(ItemRegistry.Create<Object>("(O)MysteryBox", random.Next(1, 3)));
        }

        list.AddRange(TrySpawnRareObject(player, tile, location, 9.0));

        foreach (var drop in drops)
        {
            if (!RandomExtensions.NextBool(random, drop.Chance))
                continue;

            if (drop.Condition != null && !GameStateQuery.CheckConditions(drop.Condition, location, player, null, null, random))
                continue;

            Item item = ItemQueryResolver.TryResolveRandomItem(drop, context, avoidRepeat: false, logError: (query, error) =>
            {
                Mod.LogDebug($"Location '{Location.NameOrUniqueName}' failed parsing item query '{query}' for artifact spot '{drop.Id}': {error}");
            });

            if (item == null)
                continue;

            if (drop.OneDebrisPerDrop && item.Stack > 1)
            {
                list.Add((Object)item);
            }
            else
            {
                for (int i = 0; i < Game1.random.Next(1, 4); i++)
                {
                    var copy = ItemRegistry.Create(item.itemId.Value, item.stack.Value);
                    list.Add((Object)copy);
                }
            }

            if (hasGenerousEnchantment && drop.ApplyGenerousEnchantment && RandomExtensions.NextBool(random))
            {
                var bonusItem = item.getOne();
                bonusItem = (Item)ItemQueryResolver.ApplyItemFields(bonusItem, drop, context);
                list.Add((Object)bonusItem);
            }

            if (!drop.ContinueOnDrop)
                break;
        }

        return list;
    }

    public static List<Object> TrySpawnRareObject(Farmer who, Vector2 position, GameLocation location, double chanceModifier = 1.0, double dailyLuckWeight = 1.0, int groundLevel = -1, Random random = null)
    {
        random ??= Game1.random;
        var list = new List<Object>();

        double luckMod = 1.0;
        if (who != null)
            luckMod = 1.0 + who.team.AverageDailyLuck() * dailyLuckWeight;

        if (who != null && who.stats.Get(StatKeys.Mastery(0)) != 0 && random.NextDouble() < 0.001 * chanceModifier * luckMod)
        {
            list.Add(ItemRegistry.Create<Object>("(O)GoldenAnimalCracker"));
        }

        if (Game1.stats.DaysPlayed > 2 && random.NextDouble() < 0.002 * chanceModifier)
        {
            var cosmetic = GetRandomCosmeticItem(Game1.random);
            if (cosmetic != null)
                list.Add(cosmetic);
        }

        if (Game1.stats.DaysPlayed > 2 && random.NextDouble() < 0.0006 * chanceModifier)
        {
            list.Add(ItemRegistry.Create<Object>("(O)SkillBook_" + Game1.random.Next(5)));
        }

        return list;
    }

    public static Object GetRandomCosmeticItem(Random r)
    {
        if (r.NextDouble() < 0.2)
        {
            if (r.NextDouble() < 0.05)
                return ItemRegistry.Create<Object>("(F)1369");

            Object item = null;
            switch (r.Next(3))
            {
                case 0:
                    item = ItemRegistry.Create<Object>(Utility.getRandomSingleTileFurniture(r));
                    break;
                case 1:
                    item = ItemRegistry.Create<Object>("(F)" + r.Next(1362, 1370));
                    break;
                case 2:
                    item = ItemRegistry.Create<Object>("(F)" + r.Next(1376, 1391));
                    break;
            }

            if (item == null || item.Name.Contains("Error"))
                item = ItemRegistry.Create<Object>("(F)1369");

            return item;
        }
        return null;
    }
}
