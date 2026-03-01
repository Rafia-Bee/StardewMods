using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace BulkDerbyRewards.Framework;

/// <summary>Generates Trout Derby prizes following the same sequence as the vanilla game.</summary>
internal static class TroutDerbyPrizes
{
    private const string ModDataTentKitGiven = "RafiaBee.BulkDerbyRewards/tentKitGiven";
    private const string ModDataPrizeIndex = "RafiaBee.BulkDerbyRewards/prizeIndex";

    /// <summary>
    /// Prize cycle after the initial Tent Kit. Order matches the wiki:
    /// Bucket Hat, Crab Pot, Mystery Box ×3, Diamond, Mounted Trout,
    /// Deluxe Bait ×20, Triple Shot Espresso ×2, Quality Sprinkler,
    /// Warp Totem: Farm ×3, Omni Geode ×3.
    /// </summary>
    private static readonly (string QualifiedId, int Quantity)[] PrizeCycle =
    {
        ("(H)BucketHat",  1),
        ("(O)710",        1),   // Crab Pot
        ("(O)MysteryBox", 3),
        ("(O)72",         1),   // Diamond
        ("(F)MountedTrout_Painting", 1),
        ("(O)DeluxeBait", 20),
        ("(O)253",        2),   // Triple Shot Espresso
        ("(O)621",        1),   // Quality Sprinkler
        ("(O)688",        3),   // Warp Totem: Farm
        ("(O)749",        3),   // Omni Geode
    };

    /// <summary>Generate prizes for the given number of Golden Tags.</summary>
    public static List<Item> GeneratePrizes(int tagCount)
    {
        var prizes = new List<Item>();
        Farmer player = Game1.player;

        bool tentKitGiven = player.modData.ContainsKey(ModDataTentKitGiven);
        int cycleIndex = GetCycleIndex(player);

        for (int i = 0; i < tagCount; i++)
        {
            if (!tentKitGiven)
            {
                Item tentKit = TryCreateItem("(O)TentKit", 1);
                if (tentKit != null)
                    prizes.Add(tentKit);

                player.modData[ModDataTentKitGiven] = "true";
                tentKitGiven = true;
                continue;
            }

            var (qualifiedId, quantity) = PrizeCycle[cycleIndex % PrizeCycle.Length];
            Item prize = TryCreateItem(qualifiedId, quantity);
            if (prize != null)
                prizes.Add(prize);

            cycleIndex++;
        }

        SetCycleIndex(player, cycleIndex % PrizeCycle.Length);
        return prizes;
    }

    /// <summary>Try to create an item, returning null on failure and logging a warning.</summary>
    private static Item TryCreateItem(string qualifiedId, int quantity)
    {
        try
        {
            if (ItemRegistry.IsQualifiedItemId(qualifiedId))
            {
                Item item = ItemRegistry.Create(qualifiedId, quantity);
                if (item != null)
                    return item;
            }
        }
        catch
        {
            // Ignored — fall through to warning.
        }

        ModEntry.ModMonitor?.Log($"Could not create item '{qualifiedId}' (qty {quantity}). "
            + "The item ID may have changed; please report this.", LogLevel.Warn);
        return null;
    }

    /// <summary>Read the player's current position in the prize cycle.</summary>
    private static int GetCycleIndex(Farmer player)
    {
        if (player.modData.TryGetValue(ModDataPrizeIndex, out string raw) && int.TryParse(raw, out int idx))
            return idx;

        // First non-Tent-Kit prize starts at a random position (vanilla behaviour).
        int startIndex = Game1.random.Next(PrizeCycle.Length);
        player.modData[ModDataPrizeIndex] = startIndex.ToString();
        return startIndex;
    }

    private static void SetCycleIndex(Farmer player, int index)
    {
        player.modData[ModDataPrizeIndex] = index.ToString();
    }

    /// <summary>Count the total number of Golden Tags in the player's inventory.</summary>
    public static int CountGoldenTags()
    {
        return Game1.player.Items
            .Where(item => item != null && IsGoldenTag(item))
            .Sum(item => item.Stack);
    }

    /// <summary>Remove all Golden Tags from the player's inventory.</summary>
    public static void RemoveAllGoldenTags()
    {
        for (int i = Game1.player.Items.Count - 1; i >= 0; i--)
        {
            if (Game1.player.Items[i] != null && IsGoldenTag(Game1.player.Items[i]))
                Game1.player.Items[i] = null;
        }
    }

    /// <summary>Remove a specific number of Golden Tags.</summary>
    public static void RemoveGoldenTags(int count)
    {
        int remaining = count;
        for (int i = Game1.player.Items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            Item item = Game1.player.Items[i];
            if (item == null || !IsGoldenTag(item))
                continue;

            if (item.Stack <= remaining)
            {
                remaining -= item.Stack;
                Game1.player.Items[i] = null;
            }
            else
            {
                item.Stack -= remaining;
                remaining = 0;
            }
        }
    }

    /// <summary>Check whether an item is a Golden Tag.</summary>
    public static bool IsGoldenTag(Item item)
    {
        // Primary check: qualified item ID.
        if (item.QualifiedItemId == "(O)TroutDerbyTag")
            return true;

        // Fallback: match by internal name in case the item ID differs.
        if (item.Name == "Golden Tag" || item.DisplayName == "Golden Tag")
            return true;

        return false;
    }
}
