namespace MoreQuests;

/// Per-quest content settings. Engine-wide tunables (questsPerDay, friendship/gold bases,
/// deadlines, reward multipliers, vanilla weights) live in the framework's own config.
public sealed class ModConfig
{
    // ----- Master toggles -----
    public bool ConsequencesEnabled { get; set; } = true;
    public bool IncludeModdedItems { get; set; } = true;
    public bool IncludeModdedNPCs { get; set; } = true;
    public bool FestivalQuestsEnabled { get; set; } = true;
    public bool AnimalQuestsEnabled { get; set; } = true;
    public bool FarmingQuestsEnabled { get; set; } = true;
    public bool FishingQuestsEnabled { get; set; } = true;
    public bool MiningQuestsEnabled { get; set; } = true;
    public bool ForagingQuestsEnabled { get; set; } = true;
    public bool CookingQuestsEnabled { get; set; } = true;
    public bool SocialQuestsEnabled { get; set; } = true;
    public bool SeasonalQuestsEnabled { get; set; } = true;
    public bool SecretGiftHintEnabled { get; set; } = true;

    /// When on, an Adventurer's Guild board renders at the Mine entrance and hosts the
    /// mining/monster quests. When off, those quests fall back to the help-wanted board.
    public bool EnableAdventurersGuildBoard { get; set; } = true;

    /// Tile X of the guild board's anchor (where the player walks to interact).
    public int AdventureBoardTileX { get; set; } = 20;
    /// Tile Y of the guild board's anchor.
    public int AdventureBoardTileY { get; set; } = 4;
    /// Pixel X offset for the in-world sprite. 1 tile = 64px. Negative values move left.
    public int AdventureBoardDrawOffsetX { get; set; } = 0;
    /// Pixel Y offset for the in-world sprite. Negative values move up.
    public int AdventureBoardDrawOffsetY { get; set; } = -197;

    // ----- Shop discounts -----
    public int ShopDiscountPercent { get; set; } = 50;
    public int ShopDiscountDurationDays { get; set; } = 2;
    public int SeedShopDiscountPercent { get; set; } = 20;
    public int SeedShopDiscountDurationDays { get; set; } = 3;

    // ----- Shared cooldown buckets -----
    /// Days a "short" bucket quest waits before re-rolling. Quests opt in via
    /// `Trigger.CooldownTier: "Short"` in quests.json. One-shot / building / mail-periodic
    /// triggers don't use the bucket system and keep their per-quest CooldownDays.
    public int QuestCooldownShortDays { get; set; } = 2;
    /// Days a "medium" bucket quest waits before re-rolling.
    public int QuestCooldownMediumDays { get; set; } = 7;
    /// Days a "long" bucket quest waits before re-rolling.
    public int QuestCooldownLongDays { get; set; } = 14;

    // ----- Quantity tunables -----
    public int FishHaulMediumQty { get; set; } = 15;
    public int FishHaulLargeQty { get; set; } = 30;
    public int CropMassiveQty { get; set; } = 50;

    /// How many distinct item variations a "mixed bag" quest asks for (e.g. Pierre's
    /// seasonal-crop spread). Clamped to [2, 5] at read time.
    public int RequestVariationCount { get; set; } = 3;

    // ----- Skull Cavern depth cap for Deep Dive quest -----
    public int SkullCavernMaxLevel { get; set; } = 100;

    // ----- Saloon weekly special / Grand Feast tunables -----
    /// Recipes with at least this many distinct ingredient lines qualify for the Complex pool.
    public int WeeklySpecialComplexMinIngredients { get; set; } = 4;

    // ----- Wrapping Paper (gated on Si.ExtraCraftingMaterials) -----
    /// Paper item id from Si's Extra Crafting Materials. Override if the source mod renames it.
    public string WrappingPaperPaperId { get; set; } = "Si.ECM_Paper";
    /// Tape item id from Si's Extra Crafting Materials.
    public string WrappingPaperTapeId { get; set; } = "Si.ECM_Tape";
    /// Book of Stars reward item id from Si's Extra Crafting Materials.
    public string WrappingPaperBookOfStarsId { get; set; } = "Si.ECM_BookOfStars";

    // ----- Gus's festival feasts -----
    /// Number of distinct ingredients Gus's Fall + Summer feasts ask for.
    public int GusFestivalFeastIngredientCount { get; set; } = 2;
    /// Tier bump applied to the governor's Luau reaction. Clamped to 5 ("loved it"); 6 is
    /// the Mayor's Shorts gag and is never overwritten.
    public int FestivalBiasLuauMagnitude { get; set; } = 1;
    /// Flat bonus added to the grange score before Lewis judges. Thresholds are 60/75/90,
    /// so 15 nudges most submissions one podium step up without guaranteeing first.
    /// Only used when FairFestivalRewardKind is GrangeScoreBonus.
    public int FestivalBiasFairMagnitude { get; set; } = 15;

    /// How the Fair decor supply quest pays out. GrangeScoreBonus adds flat points to the
    /// grange display score. StarTokens skips the bias and injects extra star tokens into
    /// festivalScore once the Fair is live. Values: GrangeScoreBonus, StarTokens.
    public string FairFestivalRewardKind { get; set; } = "GrangeScoreBonus";

    /// Bonus star tokens granted at the Fair when FairFestivalRewardKind is StarTokens.
    /// Default 100 is one mid-priced shop item. Ignored for grange bonus.
    public int FairStarTokensAmount { get; set; } = 100;

    // ----- Festival decor-supply quests -----
    /// East Scarp NPCs (comma-separated) that get a FriendshipMultiHeart bump on completing
    /// the East Scarp Spirit's Eve quest. Missing NPCs silently no-op so over-listing is safe.
    public string EastScarpFestivalNpcs { get; set; } = "Sonny, Rosa, Eli, Andy, Bonnie, Lily";
    /// Ridgeside Village NPCs (comma-separated) that get a FriendshipMultiHeart bump on
    /// completing the Ridgeside Gathering quest.
    public string RidgesideFestivalNpcs { get; set; } = "Pika, Lenny, Mr. Aguar, Pam, Blair, Kimpoi, Hugo";

    /// Tub o' Flowers item id (RSV Gathering's primary ship item). Default is vanilla (BC)108.
    public string RsvTubOFlowersId { get; set; } = "(BC)108";
    /// Tub o' Flowers crafting recipe name. Granted on quest-accept if the player doesn't know it.
    public string RsvTubOFlowersRecipeName { get; set; } = "Tub o' Flowers";

    // ----- Fishing-track tunables -----
    /// Chance (0 to 100) that Rainy Day Catch mail lands when tomorrow is forecast rain.
    /// 100 = always when rain is forecast, 0 = disables the quest.
    public int RainyDayCatchMailChancePercent { get; set; } = 100;

    /// Trout Derby recipe for Gus / vanilla saloon. Vanilla Rainbow Trout recipe is Trout Soup.
    public string TroutDerbyRecipeGus { get; set; } = "Trout Soup";
    /// Trout Derby recipe for Pika (RSV).
    public string TroutDerbyRecipePika { get; set; } = "Highland Ice Cream";
    /// Trout Derby recipe for Celestine (Visit Mount Vapius).
    public string TroutDerbyRecipeCelestine { get; set; } = "Toast and Trout";
    /// Trout Derby recipe for Rosa (East Scarp). Falls back to vanilla Trout Soup.
    public string TroutDerbyRecipeRosa { get; set; } = "Trout Soup";

    /// Shop ids used by the Trout Derby ShopDiscountReward per giver. Empty value
    /// skips the discount for that giver but still grants the recipe.
    public string TroutDerbyShopGus { get; set; } = "Saloon";
    public string TroutDerbyShopPika { get; set; } = "RSVPikaShop";
    public string TroutDerbyShopRosa { get; set; } = "Lemurkat.EastScarp_InnShop";
    public string TroutDerbyShopCelestine { get; set; } = "Saloon";

    /// Qualified item id of the discounted dish per giver. Defaults reference the
    /// vanilla / RSV / VMV dishes that ship with the matching recipe; override to
    /// match your modded recipe pack. Empty value skips the discount.
    public string TroutDerbyDishGus { get; set; } = "(O)219";
    public string TroutDerbyDishPika { get; set; } = "(O)Rafseazz.RSVCP_Highland_Ice_Cream";
    public string TroutDerbyDishRosa { get; set; } = "(O)219";
    public string TroutDerbyDishCelestine { get; set; } = "(O)Lumisteria.MtVapius_Cooking_TroutAndToast";

    /// SquidFest recipe for Gus / vanilla saloon.
    public string SquidFestRecipeGus { get; set; } = "Fried Calamari";
    /// SquidFest recipe for Pika (RSV).
    public string SquidFestRecipePika { get; set; } = "Ridgeside Shaketini";
    /// SquidFest recipe for Celestine (VMV).
    public string SquidFestRecipeCelestine { get; set; } = "Squid Ink Ravioli";
    /// SquidFest recipe for Rosa (East Scarp).
    public string SquidFestRecipeRosa { get; set; } = "Fried Calamari";

    /// Shop ids used by the SquidFest ShopDiscountReward per giver. Empty value
    /// skips the discount for that giver but still grants the recipe.
    public string SquidFestShopGus { get; set; } = "Saloon";
    public string SquidFestShopPika { get; set; } = "RSVPikaShop";
    public string SquidFestShopRosa { get; set; } = "Lemurkat.EastScarp_InnShop";
    public string SquidFestShopCelestine { get; set; } = "Saloon";

    /// Qualified item id of the discounted dish per giver. Defaults reference the
    /// vanilla / RSV / ES dishes that ship with the matching recipe; override to
    /// match your modded recipe pack. Empty value skips the discount.
    public string SquidFestDishGus { get; set; } = "(O)227";
    public string SquidFestDishPika { get; set; } = "(O)Rafseazz.RSVCP_Ridgeside_Shaketini";
    public string SquidFestDishRosa { get; set; } = "(O)228";
    public string SquidFestDishCelestine { get; set; } = "";

    // ----- One-shot triggered animal/farm quests -----

    /// Alex's Protein Shakes: final qty = Base + chickens * PerChicken, clamped to [Base, 30].
    /// Set PerChicken to 0 to always ask for exactly Base eggs.
    public int AlexProteinShakesBaseQty { get; set; } = 5;
    public int AlexProteinShakesPerChicken { get; set; } = 1;

    /// Gold rebate paid on Marnie's Chicken Offer completion. Vanilla white chicken is 800g.
    /// Done as a rebate since the framework doesn't currently hook PurchaseAnimalsMenu.
    public int MarnieChickenOfferRebate { get; set; } = 800;
    /// Seed delivery qty for the chicken-offer quest. Picks one current-season seed.
    public int MarnieChickenOfferSeedQty { get; set; } = 15;

    /// Gold rebate for Marnie's Cow Offer (vanilla cow is 1500g).
    public int MarnieCowOfferRebate { get; set; } = 1500;
    /// Hay qty Marnie's Cow Offer asks for. Hay sells for 50g, so 50 hay is meaningful but not punishing.
    public int MarnieCowOfferHayQty { get; set; } = 50;

    /// Egg qty for Marnie's Egg Request. Quest grants the Mayonnaise Machine recipe.
    public int MarnieEggRequestQty { get; set; } = 10;
    /// Milk qty for Marnie's Milk Request. Quest grants the Cheese Press recipe.
    public int MarnieMilkRequestQty { get; set; } = 10;

    /// Stone qty Robin asks for in her silo offer. Player can satisfy with stone, clay, or
    /// copper bars (vanilla silo recipe: 100 stone OR 10 clay OR 5 copper bars).
    public int RobinSiloOfferStoneQty { get; set; } = 100;
    public int RobinSiloOfferClayQty { get; set; } = 10;
    public int RobinSiloOfferCopperBarQty { get; set; } = 5;

    // ----- Multi-step / misc quest tunables -----

    /// Spring Cleaning weed-clear count. Targets `'*', '!Farm'` (wildcard avoids
    /// "this location has no weeds today" dead-ends).
    public int SpringCleaningCount { get; set; } = 8;

    /// Check on Friends: how many distinct NPCs to talk to before reporting back.
    public int CheckOnFriendsCount { get; set; } = 3;
}
