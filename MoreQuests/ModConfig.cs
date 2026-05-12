namespace MoreQuests;

/// Per-quest content settings for the More Quests content mod. Engine-wide tunables
/// (questsPerDay, friendship/gold bases, deadlines, reward multipliers, vanilla quest
/// weights) live in the framework's own config under `Mods/MoreQuestsFramework/config.json`.
public sealed class ModConfig
{
    // ----- Master toggles -----
    public bool ConsequencesEnabled { get; set; } = true;
    public bool IncludeModdedItems { get; set; } = true;
    public bool IncludeModdedNPCs { get; set; } = true;
    public bool FestivalQuestsEnabled { get; set; } = true;
    public bool AnimalQuestsEnabled { get; set; } = true;
    public bool SecretGiftHintEnabled { get; set; } = true;

    /// When on (default), an Adventurer's Guild board renders at the Mine entrance and
    /// hosts the mining + monster quests (Mines / Skull Cavern Deep Dive, Basic Slime
    /// Clearing, Vanilla monster eradication); the help-wanted board only sees Bar
    /// Delivery from the mining category. When off, the guild board is hidden entirely
    /// and every guild-tagged quest falls back to the help-wanted board so the content
    /// stays reachable. Per-quest weights still gate individual quests on top of this.
    public bool EnableAdventurersGuildBoard { get; set; } = true;

    /// Tile X of the Adventurer's Guild board's anchor (the player walks here to interact).
    /// Default places the anchor on a reachable Mine entrance tile beneath the wall art;
    /// tunable from GMCM so players can move the board after installing other Mine-modifying mods.
    public int AdventureBoardTileX { get; set; } = 20;
    /// Tile Y of the Adventurer's Guild board's anchor.
    public int AdventureBoardTileY { get; set; } = 4;
    /// Pixel X offset applied to the in-world board sprite (and the indicator). 1 tile = 64px.
    /// Lets players park the sprite art on a nearby wall while keeping the click target at
    /// a walkable spot. Negative values move left.
    public int AdventureBoardDrawOffsetX { get; set; } = 0;
    /// Pixel Y offset applied to the in-world board sprite. Negative values move up. Default
    /// lifts the sprite onto the Mine wall above the anchor tile.
    public int AdventureBoardDrawOffsetY { get; set; } = -197;

    // ----- Shop discounts -----
    public int ShopDiscountPercent { get; set; } = 50;
    public int ShopDiscountDurationDays { get; set; } = 2;
    public int SeedShopDiscountPercent { get; set; } = 20;
    public int SeedShopDiscountDurationDays { get; set; } = 3;

    // ----- Shared cooldown buckets -----
    /// In-game days a quest in the "short" cooldown bucket waits before re-rolling. Quests
    /// in `assets/quests.json` opt in via `Trigger.CooldownTier: "Short"` (the framework
    /// resolves the tier name to this value at trigger evaluation time, so GMCM edits apply
    /// without re-rolling the day). One-shot / building / mail-periodic triggers are not in
    /// the bucket system and keep their per-quest `CooldownDays`.
    public int QuestCooldownShortDays { get; set; } = 2;
    /// In-game days a quest in the "medium" cooldown bucket waits before re-rolling.
    public int QuestCooldownMediumDays { get; set; } = 7;
    /// In-game days a quest in the "long" cooldown bucket waits before re-rolling.
    public int QuestCooldownLongDays { get; set; } = 14;

    // ----- Quantity tunables -----
    public int FishHaulMediumQty { get; set; } = 15;
    public int FishHaulLargeQty { get; set; } = 30;
    public int FestivalFishQty { get; set; } = 5;
    public int CropMassiveQty { get; set; } = 50;
    public int HaySupplyBaseQty { get; set; } = 10;

    /// Shared knob for "how many distinct item variations does an NPC request" across
    /// quests that ask for a mixed bag (e.g. Pierre's Stock-Up's seasonal-crop spread).
    /// Clamped to [2, 5] at read time. Default 3 matches the prior hardcoded value.
    public int RequestVariationCount { get; set; } = 3;

    // ----- Skull Cavern depth cap for Deep Dive quest -----
    public int SkullCavernMaxLevel { get; set; } = 100;

    // ----- Saloon weekly special / Grand Feast tunables -----
    /// Recipes with at most this many distinct ingredient lines qualify for the Common pool.
    public int WeeklySpecialCommonMaxIngredients { get; set; } = 3;
    /// Recipes with at least this many distinct ingredient lines qualify for the Complex pool.
    public int WeeklySpecialComplexMinIngredients { get; set; } = 4;
    /// Number of distinct recipes a Grand Feast SpecialOrder asks for.
    public int GrandFeastRecipeCount { get; set; } = 3;

    // ----- Phase 9.5a: Wrapping Paper (mod-gated on Si.ExtraCraftingMaterials) -----
    /// Qualified item id for the Paper item from Si's Extra Crafting Materials. Override
    /// here if the source mod renames the item between versions.
    public string WrappingPaperPaperId { get; set; } = "Si.ECM_Paper";
    /// Qualified item id for the Tape item from Si's Extra Crafting Materials.
    public string WrappingPaperTapeId { get; set; } = "Si.ECM_Tape";
    /// Qualified item id for the Book of Stars reward item from Si's Extra Crafting Materials.
    public string WrappingPaperBookOfStarsId { get; set; } = "Si.ECM_BookOfStars";

    // ----- Phase 9d: Gus's festival feasts -----
    /// Number of distinct ingredient kinds Gus's Fall + Summer feasts ask for.
    public int GusFestivalFeastIngredientCount { get; set; } = 3;
    /// Tier bump applied to the governor's Luau reaction (clamped to 5 = "loved it"; 6 is
    /// the Mayor's Shorts gag and is never overwritten). 1 = one tier up.
    public int FestivalBiasLuauMagnitude { get; set; } = 1;
    /// Flat bonus added to the player's Stardew Valley Fair grange score before Lewis
    /// judges. The pass-the-test threshold is 60 / podium tiers at 75 + 90, so 15 nudges
    /// most submissions one podium step up without forcing a guaranteed first.
    public int FestivalBiasFairMagnitude { get; set; } = 15;

    // ----- Phase 9.5d: Festival decor-supply quests -----
    /// Comma-separated list of East Scarp NPC names that get a `FriendshipMultiHeart` bump
    /// on completing the East Scarp Spirit's Eve festival quest (CSV row 21). Curated to
    /// well-known mod NPCs; users can extend or override. Friendships to NPCs the save
    /// doesn't have silently no-op, so over-listing is safe.
    public string EastScarpFestivalNpcs { get; set; } = "Sonny, Rosa, Eli, Andy, Bonnie, Lily";
    /// Comma-separated list of Ridgeside Village NPC names that get a `FriendshipMultiHeart`
    /// bump on completing the Ridgeside Gathering quest (CSV row 25). Same conventions as
    /// `EastScarpFestivalNpcs`.
    public string RidgesideFestivalNpcs { get; set; } = "Pika, Lenny, Mr. Aguar, Pam, Blair, Kimpoi, Hugo";

    /// Qualified item id for Tub o' Flowers, the RSV Gathering quest's primary ship item.
    /// Defaults to vanilla Stardew's Tub o' Flowers `(BC)272`. RSV ships its own variant
    /// via a different id; override here if your save uses the RSV-namespaced item.
    public string RsvTubOFlowersId { get; set; } = "(BC)272";
    /// Crafting recipe name for Tub o' Flowers. Granted via `Player.craftingRecipes.Add`
    /// at quest-accept if the player doesn't already know it. Defaults to vanilla "Tub o' Flowers".
    public string RsvTubOFlowersRecipeName { get; set; } = "Tub o' Flowers";

    // ----- Phase 9.5e: Fishing-track tunables -----
    /// Probability (0 to 100) that the Rainy Day Catch mail lands on a day where tomorrow
    /// is forecast as rain. The quest no longer has a daily-board variant; this knob is
    /// the only spawn gate apart from the Fishing 3 skill requirement. 100 = always mails
    /// when rain is forecast (legacy behavior), 0 disables the quest entirely.
    public int RainyDayCatchMailChancePercent { get; set; } = 100;

    /// Trout Derby (Rainbow Platter) recipe granted to Gus / vanilla saloon-chef saves.
    /// Vanilla cooking recipes that use Rainbow Trout: "Trout Soup". Authors can override
    /// to "Maki Roll" or any other recipe name. RecipeKind defaults to Cooking.
    public string TroutDerbyRecipeGus { get; set; } = "Trout Soup";
    /// Trout Derby recipe for Pika (Ridgeside Village). RSV recipe; the framework grants
    /// via `RecipeReward` so authors can swap if a content pack renames the recipe.
    public string TroutDerbyRecipePika { get; set; } = "Highland Ice Cream";
    /// Trout Derby recipe for Celestine (Visit Mount Vapius). VMV recipe.
    public string TroutDerbyRecipeCelestine { get; set; } = "Toast and Trout";
    /// Trout Derby recipe for Rosa (East Scarp). Falls back to vanilla Trout Soup unless
    /// East Scarp ships a trout-derby-specific dish — override here when one lands.
    public string TroutDerbyRecipeRosa { get; set; } = "Trout Soup";
    /// Qualified item id for the Gus Trout Derby reward dish, used by `ShopDiscountReward`
    /// to discount the dish in the Saloon shop. Defaults to vanilla Trout Soup `(O)219`.
    /// Pair with `TroutDerbyRecipeGus`.
    public string TroutDerbyDishGus { get; set; } = "(O)219";

    /// SquidFest recipe for Gus / vanilla saloon. Vanilla recipes using Squid: "Squid Ink
    /// Ravioli" (uses Squid Ink) or "Fried Calamari".
    public string SquidFestRecipeGus { get; set; } = "Fried Calamari";
    /// SquidFest recipe for Pika (RSV). RSV recipe.
    public string SquidFestRecipePika { get; set; } = "Ridgeside Shaketini";
    /// SquidFest recipe for Celestine (VMV).
    public string SquidFestRecipeCelestine { get; set; } = "Squid Ink Ravioli";
    /// SquidFest recipe for Rosa (East Scarp).
    public string SquidFestRecipeRosa { get; set; } = "Fried Calamari";
    /// Qualified item id for the Gus SquidFest reward dish, used by `ShopDiscountReward`
    /// to discount the dish in the Saloon shop. Defaults to Fried Calamari `(O)227`.
    public string SquidFestDishGus { get; set; } = "(O)227";

    // ----- Phase 9.5f: One-shot triggered animal/farm quests -----

    /// Alex's Protein Shakes (CSV row 14): how many eggs to ask for per chicken on the
    /// farm, plus a flat base. Final qty = `AlexProteinShakesBaseQty` + chickens *
    /// `AlexProteinShakesPerChicken`, clamped between Base and 30. Set per-chicken to 0
    /// to drop the scaling and always ask for exactly Base eggs.
    public int AlexProteinShakesBaseQty { get; set; } = 5;
    public int AlexProteinShakesPerChicken { get; set; } = 1;

    /// Gold rebate for Marnie's Chicken Offer (CSV row 43). Vanilla white chicken price
    /// is 800g; the quest pays the player back this amount on completion to simulate
    /// "get a deal on a chicken" without patching the animal-shop menu (deferred — the
    /// framework doesn't currently hook `PurchaseAnimalsMenu`).
    public int MarnieChickenOfferRebate { get; set; } = 800;
    /// How many seeds the chicken-offer quest asks the player to deliver. The quest picks
    /// one current-season seed type and asks for this many.
    public int MarnieChickenOfferSeedQty { get; set; } = 15;

    /// Gold rebate for Marnie's Cow Offer (CSV row 44). Vanilla cow price is 1500g; same
    /// rebate-as-proxy approach as the Chicken Offer.
    public int MarnieCowOfferRebate { get; set; } = 1500;
    /// How many Hay the cow-offer quest asks the player to deliver. Vanilla hay sells for
    /// 50g, so 50 hay is a meaningful (but not punishing) ask paired with the cow rebate.
    public int MarnieCowOfferHayQty { get; set; } = 50;

    /// How many eggs Marnie's Egg Request (CSV row 45) asks the player to ship. Defaults
    /// to 10 per the CSV. Quest grants the Mayonnaise Machine recipe as part of the reward.
    public int MarnieEggRequestQty { get; set; } = 10;
    /// How many milk units Marnie's Milk Request (CSV row 47) asks the player to ship.
    /// Defaults to 10 per the CSV. Quest grants the Cheese Press recipe as part of the reward.
    public int MarnieMilkRequestQty { get; set; } = 10;

    /// Gold rebate for Robin's Silo Offer (CSV row 64). Vanilla silo costs 100g + 100
    /// Stone + 10 Clay + 5 Copper Bar. The rebate covers the gold portion plus a chunk
    /// toward the materials, since the player still has to pay Robin themselves (the
    /// "Robin contributes" flavor in the CSV is approximated by the reimbursement).
    public int RobinSiloOfferRebate { get; set; } = 500;
    /// Stone quantity Robin asks for in her silo offer letter. The framework picks one of
    /// stone / clay / copper bar to keep the request varied; see `RobinSiloOfferClayQty` /
    /// `RobinSiloOfferCopperBarQty` for the alternatives.
    public int RobinSiloOfferStoneQty { get; set; } = 100;
    public int RobinSiloOfferClayQty { get; set; } = 10;
    public int RobinSiloOfferCopperBarQty { get; set; } = 5;

    // ----- Phase 9.5g: Multi-step / misc quest tunables -----

    /// Clear Debris (CSV row 17). Location whose `ResourceClumps` the player must remove
    /// before the quest closes. Defaults to vanilla Pelican Town. Override per-save if a
    /// modded NPC's home town should host the quest instead.
    public string ClearDebrisLocation { get; set; } = "Town";
    /// How many resource clumps the quest asks the player to clear at the target location.
    public int ClearDebrisCount { get; set; } = 5;

    /// Dinner Party (CSV row 18). Number of distinct dishes the giver requests, picked
    /// from dishes the giver Loves or Likes per `Data/NPCGiftTastes`. Per-dish quantity
    /// defaults to 1 to keep the order playable, since each dish has to be cooked.
    public int DinnerPartyDishCount { get; set; } = 3;
    public int DinnerPartyPerDishCount { get; set; } = 1;

    /// Plant Trees (CSV row 55). Location where new trees must be planted before the
    /// step closes. Defaults to vanilla Cindersap Forest; modded conservation NPCs
    /// (Kimpoi/Dylan/Aster) currently route to the same fallback until their respective
    /// content packs expose a stable per-NPC location id.
    public string PlantTreesLocation { get; set; } = "Forest";
    public int PlantTreesCount { get; set; } = 5;

    /// Spring Cleaning (CSV row 69). Spring-only town weed clear. Location and weed count
    /// are configurable for parity with the other 9.5g rows.
    public string SpringCleaningLocation { get; set; } = "Town";
    public int SpringCleaningCount { get; set; } = 8;
}
