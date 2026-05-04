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

    // ----- Shop discounts -----
    public int ShopDiscountPercent { get; set; } = 50;
    public int ShopDiscountDurationDays { get; set; } = 2;
    public int SeedShopDiscountPercent { get; set; } = 20;
    public int SeedShopDiscountDurationDays { get; set; } = 3;

    // ----- Quantity tunables -----
    public int FishHaulMediumQty { get; set; } = 15;
    public int FishHaulLargeQty { get; set; } = 30;
    public int FestivalFishQty { get; set; } = 5;
    public int CropMassiveQty { get; set; } = 50;
    public int HaySupplyBaseQty { get; set; } = 10;

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
    /// Inches threshold for the "Small" bucket of the Size Fish Overpopulation quest.
    /// A catch with reported size in `[1, SizeBucketSmallMaxInches]` counts toward the
    /// Small bucket. Defaults match vanilla Data/Fish max sizes for small species (perch /
    /// chub / smallmouth bass land at 12-16 inches).
    public int SizeBucketSmallMaxInches { get; set; } = 12;
    /// Inches threshold separating Medium from Large. Catches in
    /// `(SizeBucketSmallMaxInches, SizeBucketMediumMaxInches]` count Medium; above counts
    /// Large. Vanilla Data/Fish max sizes cluster around 24-30 inches at the medium-large
    /// boundary (carp, salmon, etc.).
    public int SizeBucketMediumMaxInches { get; set; } = 24;

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
}
