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
}
