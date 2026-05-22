using System.Linq;
using MoreQuests.Quests;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuests;

/// Bridges Milk Pail purchases at Marnie's shop into PurchaseFromShopQuest. A Milk
/// Pail is a Tool, so the framework's ItemDelivery flow can't see it. Watching
/// InventoryChanged while Marnie's ShopMenu is open is the cleanest hook (no
/// Harmony required, third-party shop replacements that still drop the item into
/// `Farmer.Items` work fine).
internal static class MarnieMilkPailHook
{
    private const string MilkPailQualifiedId = "(T)MilkPail";
    private const string MarnieName = "Marnie";

    internal static void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!e.IsLocalPlayer)
            return;

        if (Game1.activeClickableMenu is not ShopMenu shop)
            return;
        if (!IsMarnieShop(shop))
            return;

        bool boughtPail = e.Added.Any(item => item != null
            && string.Equals(item.QualifiedItemId, MilkPailQualifiedId, System.StringComparison.OrdinalIgnoreCase));
        if (!boughtPail)
            return;

        TryCompleteActiveQuest();
    }

    private static bool IsMarnieShop(ShopMenu shop)
    {
        return string.Equals(shop.ShopId, "AnimalShop", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void TryCompleteActiveQuest()
    {
        var log = Game1.player?.questLog;
        if (log == null) return;
        foreach (var quest in log)
        {
            if (quest is PurchaseFromShopQuest p && p.Matches(MarnieName, MilkPailQualifiedId))
            {
                p.CompletePurchase();
                return;
            }
        }
    }
}
