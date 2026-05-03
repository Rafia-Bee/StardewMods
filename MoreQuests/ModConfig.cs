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
}
