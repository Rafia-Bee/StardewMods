using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace MoreQuests;

/// Harmony patch that consumes the Marnie chicken / cow / pet purchase credit on adoption.
/// The price discount itself is applied via an asset edit on `Data/FarmAnimals` (chicken/cow)
/// or `Data/Pets` (pet license). Patches `AnimalHouse.adoptAnimal` for the chicken/cow flow
/// (also covers Livestock Bazaar's replacement menu), and `PetLicense.actionWhenPurchased`
/// for the pet license flow.
internal static class MarniePurchasePatches
{
    internal const string ChickenCreditKey = "RafiaBee.MoreQuests.MarnieChickenCreditExpiry";
    internal const string CowCreditKey = "RafiaBee.MoreQuests.MarnieCowCreditExpiry";
    internal const string PetCreditKey = "RafiaBee.MoreQuests.MarniePetCreditExpiry";
    internal const string FreeChickenAnimalType = "White Chicken";
    internal const string FreeCowAnimalType = "White Cow";

    internal static void Apply(Harmony harmony, IMonitor monitor)
    {
        var target = AccessTools.Method(typeof(AnimalHouse), nameof(AnimalHouse.adoptAnimal));
        if (target == null)
        {
            monitor.Log("Couldn't find AnimalHouse.adoptAnimal; Marnie credit redemption disabled.", LogLevel.Warn);
        }
        else
        {
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MarniePurchasePatches), nameof(AfterAdoptAnimal)));
        }

        var petTarget = AccessTools.Method(typeof(PetLicense), nameof(PetLicense.actionWhenPurchased));
        if (petTarget == null)
        {
            monitor.Log("Couldn't find PetLicense.actionWhenPurchased; Marnie pet credit redemption disabled.", LogLevel.Warn);
        }
        else
        {
            harmony.Patch(petTarget, postfix: new HarmonyMethod(typeof(MarniePurchasePatches), nameof(AfterPetLicensePurchased)));
        }
    }

    private static void AfterPetLicensePurchased()
    {
        var player = Game1.player;
        if (player == null)
            return;
        if (!player.modData.ContainsKey(PetCreditKey))
            return;
        player.modData.Remove(PetCreditKey);
        ModEntry.Instance.Helper.GameContent.InvalidateCache("Data/Pets");
        Game1.addHUDMessage(new HUDMessage(
            ModEntry.I18n.Get("quest.foraging.feedWildCritters.creditRedeemed").ToString(),
            HUDMessage.achievement_type));
    }

    private static void AfterAdoptAnimal(FarmAnimal animal)
    {
        if (animal == null)
            return;

        var player = Game1.player;
        if (player == null)
            return;

        bool hasChickenCredit = player.modData.ContainsKey(ChickenCreditKey);
        bool hasCowCredit = player.modData.ContainsKey(CowCreditKey);
        if (!hasChickenCredit && !hasCowCredit)
            return;

        // `AnimalHouse.adoptAnimal` is also called from `addNewHatchedAnimal` (incubator)
        // and pregnancy births. Those show a `NamingMenu` or run under a `DialogueBox`;
        // any other active menu we treat as a purchase context.
        var menu = Game1.activeClickableMenu;
        if (menu == null)
            return;
        if (menu is NamingMenu || menu is DialogueBox)
            return;

        bool isVanillaMenu = menu is PurchaseAnimalsMenu;
        string type = animal.type?.Value ?? string.Empty;
        string house = animal.buildingTypeILiveIn?.Value ?? string.Empty;
        bool isCoopAnimal = house.IndexOf("Coop", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isBarnAnimal = !isCoopAnimal && house.IndexOf("Barn", StringComparison.OrdinalIgnoreCase) >= 0;

        // Vanilla lists each variant separately, so only the explicitly-zeroed White
        // Chicken / White Cow consumes the credit. Third-party menus (LB) collapse
        // variants via `FarmAnimalData.AlternatePurchaseTypes` and show one shared
        // price (the primary's, which we zeroed), so any coop/barn adoption from those
        // menus counts as the redemption regardless of the variant the player picked.
        bool consumeChicken = hasChickenCredit
            && (isVanillaMenu
                ? string.Equals(type, FreeChickenAnimalType, StringComparison.OrdinalIgnoreCase)
                : isCoopAnimal);
        bool consumeCow = hasCowCredit
            && (isVanillaMenu
                ? string.Equals(type, FreeCowAnimalType, StringComparison.OrdinalIgnoreCase)
                : isBarnAnimal);

        if (consumeChicken)
        {
            player.modData.Remove(ChickenCreditKey);
            ModEntry.Instance.Helper.GameContent.InvalidateCache("Data/FarmAnimals");
            Game1.addHUDMessage(new HUDMessage(
                ModEntry.I18n.Get("quest.animal.marnieChickenOffer.creditRedeemed").ToString(),
                HUDMessage.achievement_type));
            ForceCloseMenuIfThirdParty(isVanillaMenu);
            return;
        }

        if (consumeCow)
        {
            player.modData.Remove(CowCreditKey);
            ModEntry.Instance.Helper.GameContent.InvalidateCache("Data/FarmAnimals");
            Game1.addHUDMessage(new HUDMessage(
                ModEntry.I18n.Get("quest.animal.marnieCowOffer.creditRedeemed").ToString(),
                HUDMessage.achievement_type));
            ForceCloseMenuIfThirdParty(isVanillaMenu);
        }
    }

    /// Vanilla's purchase menu warps the player back to Marnie after every buy, so the
    /// asset-cache invalidation lands on the next menu open. LB keeps its menu open and
    /// caches each entry's `TradePrice` at construction time, so we have to force-close
    /// to make the next purchase rebuild entries at the post-credit price. The HUD hint
    /// tells the player why their shop window just slammed shut.
    private static void ForceCloseMenuIfThirdParty(bool isVanillaMenu)
    {
        if (isVanillaMenu)
            return;
        Game1.activeClickableMenu?.exitThisMenu();
        Game1.addHUDMessage(new HUDMessage(
            ModEntry.I18n.Get("quest.animal.marnie.menuRefreshed").ToString(),
            HUDMessage.newQuest_type));
    }
}
