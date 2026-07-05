namespace MoreQuests;

/// Per-quest content settings. Engine-wide tunables (questsPerDay, friendship/gold bases,
/// deadlines, reward multipliers, vanilla weights) live in the framework's own config.
public sealed class ModConfig
{
    // ----- Master toggles -----
    // Per-category enable switches and the consequence master switch moved to the framework
    // config (MoreQuestsFramework). The framework gates postings by category and honors the
    // consequence toggle, so both apply to any quest pack, not just this mod.
    public bool SecretGiftHintEnabled { get; set; } = true;

    /// When on, an Adventurer's Guild board renders at the Mine entrance and hosts the
    /// mining/monster quests. When off, those quests fall back to the help-wanted board.
    public bool EnableAdventurersGuildBoard { get; set; } = true;

    /// Tile X of the guild board's bottom-left corner. The board sprite grows up and to the
    /// right from here. The board lives in the Mine entrance map, on the right wall near the
    /// mine shaft. Use the Offset sliders for pixel-level nudging.
    public int AdventureBoardTileX { get; set; } = 19;
    /// Tile Y of the guild board's bottom-left corner. See AdventureBoardTileX.
    public int AdventureBoardTileY { get; set; } = 3;

    /// Pixel nudge for the guild board on top of the tile placement. Positive X moves it
    /// right, positive Y moves it down. Handy for lining the sprite up with the wall art.
    public int AdventureBoardOffsetX { get; set; } = 32;
    public int AdventureBoardOffsetY { get; set; } = 0;

    // ----- Shop discounts -----
    public int ShopDiscountPercent { get; set; } = 50;
    public int ShopDiscountDurationDays { get; set; } = 2;
    public int SeedShopDiscountPercent { get; set; } = 20;
    public int SeedShopDiscountDurationDays { get; set; } = 3;

    // ----- Quantity tunables -----
    public int FishHaulMediumQty { get; set; } = 15;
    public int FishHaulLargeQty { get; set; } = 30;
    public int CropMassiveQty { get; set; } = 50;

    /// "Know your waters": the most distinct fish a single quest will ask you to catch. The
    /// quest normally lists every fish that lives at the chosen spot this season, which can
    /// balloon once fish mods are installed. Set this above 0 to trim the list down to a
    /// random pick of that many fish. 0 (the default) means no limit, so you catch them all.
    /// Negatives are treated the same as 0.
    public int KnowYourWatersMaxFish { get; set; } = 0;

    /// Joja "Quality control" quest (Morris arc Step 2): a crop only counts as cheap junk
    /// if its base sell price is under this. Raise it if other mods bump crop values so
    /// there are still crops cheap enough to qualify. Clamped to at least 1 at read time.
    public int MorrisQualityControlMaxCropPrice { get; set; } = 30;

    /// How many distinct item variations a "mixed bag" quest asks for (e.g. Pierre's
    /// seasonal-crop spread). Clamped to [2, 5] at read time.
    public int RequestVariationCount { get; set; } = 3;

    // ----- Skull Cavern depth cap for Deep Dive quest -----
    public int SkullCavernMaxLevel { get; set; } = 100;

    /// Radius (in tiles) of the ritual circle for the Unseen Offering quest. Clamped to
    /// [2, 6] at read time (mine zones cap lower since floors are cramped). Bigger = easier
    /// to find a spot inside, smaller = a tighter target.
    public int MagicianDropRadius { get; set; } = 4;

    // ----- Saloon weekly special / Grand Feast tunables -----
    /// Recipes with at least this many distinct ingredient lines qualify for the Complex pool.
    public int WeeklySpecialComplexMinIngredients { get; set; } = 4;

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

    /// Gold paid out if the chicken-offer credit expires before the player redeems it at
    /// Marnie's shop. 800g matches the vanilla White Chicken price.
    public int MarnieChickenOfferRebate { get; set; } = 800;
    /// Seed delivery qty for the chicken-offer quest. Picks one current-season seed.
    public int MarnieChickenOfferSeedQty { get; set; } = 15;

    /// Gold paid out if the cow-offer credit expires before the player redeems it at Marnie's
    /// shop. 1500g matches the vanilla Cow price.
    public int MarnieCowOfferRebate { get; set; } = 1500;
    /// Hay qty Marnie's Cow Offer asks for. Hay sells for 50g, so 50 hay is meaningful but not punishing.
    public int MarnieCowOfferHayQty { get; set; } = 50;

    /// In-game days the chicken / cow purchase credit stays redeemable before it falls back
    /// to the gold rebate.
    public int MarnieCreditExpiryDays { get; set; } = 14;

    /// Percent off the pet license adoption price Marnie gives after the player completes
    /// three Feed Wild Critters quests. Clamped 1..100 at read time. Applies to every breed
    /// in Data/Pets while the credit is active, consumed on the next pet purchase.
    public int MarniePetDiscountPercent { get; set; } = 25;
    /// In-game days the Marnie pet discount credit stays redeemable before it silently expires.
    public int MarniePetCreditExpiryDays { get; set; } = 14;
    /// How many Feed Wild Critters completions earn one pet discount credit. Bumped on
    /// quest completion; on the Nth, Marnie issues the credit and the counter resets.
    public int FeedWildCrittersPerPetCredit { get; set; } = 3;

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

    /// Minimum Farming level the player needs before Crop Cycle starts posting. Default 4
    /// so the quest shows up by late spring year 1 but isn't spamming new saves.
    public int CropCycleMinFarmingLevel { get; set; } = 4;

    /// Frame style the player wants on Leah's farm-animal painting reward.
    /// Allowed values: "Wood", "Burgundy", "Night". The animal in the painting is
    /// always random; only the frame is player-controlled.
    public string LeahPaintingFrame { get; set; } = "Wood";

    /// Check on Friends: how many distinct NPCs to talk to before reporting back.
    public int CheckOnFriendsCount { get; set; } = 3;

    /// Emily's Housewarming: how many furniture pieces to place in the farmhouse while the
    /// quest is active (on top of needing a rug, a light source, and a wall decoration).
    public int EmilyHousewarmingCount { get; set; } = 8;

    // ----- Redecorate quest (needs Build Placement Unlocker installed to post) -----
    // A villager hands over a budget, you buy furniture and place it in their home, and
    // whatever you don't spend goes back to them. Defaults are tuned so a careful shopper
    // stays under budget and the leftover that returns to the NPC is small.

    /// Relative weight of the Redecorate quest in the daily-board pool. 0 disables it.
    public int RedecorateQuestWeight { get; set; } = 12;
    /// Days the player has to finish once they accept.
    public int RedecorateDeadlineDays { get; set; } = 7;
    /// Days before the same NPC can post another redecoration quest.
    public int RedecorateCooldownDays { get; set; } = 14;
    /// How many distinct furniture objectives a quest asks for.
    public int RedecorateMinObjectives { get; set; } = 2;
    public int RedecorateMaxObjectives { get; set; } = 3;
    /// How many pieces each objective asks for.
    public int RedecorateMinPerObjective { get; set; } = 1;
    public int RedecorateMaxPerObjective { get; set; } = 2;
    /// Budget = (sum of reference prices x counts) x generosity, rounded to the nearest 100.
    /// Above 1.0 leaves the player a little slack so staying under budget is achievable.
    public double RedecorateBudgetGenerosity { get; set; } = 1.3;
    /// Reference shop prices per category, used only to size the budget.
    public int RedecorateReferenceLampPrice { get; set; } = 1000;
    public int RedecorateReferenceRugPrice { get; set; } = 1000;
    public int RedecorateReferenceChairPrice { get; set; } = 1000;
    public int RedecorateReferenceTablePrice { get; set; } = 1500;
    /// How many reward items the giver hands over (loved first, then liked).
    public int RedecorateRewardItemCount { get; set; } = 5;

    // ----- Item exclusion list -----

    /// Items that should never be handed to the player as a quest reward. Comma-separated
    /// qualified ids (e.g. "(O)74, (O)889"). Whatever you list here is dropped from every
    /// quest reward, including quests written specifically to give that item. The default
    /// keeps the Prismatic Shard and the two Qi crops out of reward pools so a quest can't
    /// randomly gift something that valuable. Always active.
    public string RewardExclusionItemIds { get; set; } = "(O)74, (O)889, (O)890";

    /// When on, the items in RewardExclusionItemIds are also kept out of item-delivery and
    /// shipping requests, so you won't be asked to bring or ship them. Off by default: the
    /// reward block is always active, but blocking requests is opt-in.
    public bool ExcludeListAppliesToRequests { get; set; } = false;

    /// When on, internal diagnostic logs are written at Trace level. Off in release builds
    /// by default so the SMAPI log stays quiet; flip on if you're chasing a bug.
    public bool DebugLogging { get; set; } = false;
}
