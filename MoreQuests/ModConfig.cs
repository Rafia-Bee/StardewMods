using System.Collections.Generic;

namespace MoreQuests;

public sealed class ModConfig
{
    // ----- Quest board -----
    public int QuestsPerDay { get; set; } = 3;

    /// Per-definition selection weight for the daily board. Keys are definition IDs
    /// (e.g. "Vanilla.ItemDelivery", "Farming.BasicCropDelivery"). Values are relative
    /// weights; 0 disables the definition. Missing keys fall back to each definition's
    /// declared DefaultWeight.
    public Dictionary<string, int> QuestWeights { get; set; } = new();

    // ----- Master toggles -----
    public bool DifficultyScaling { get; set; } = true;
    public bool ConsequencesEnabled { get; set; } = true;
    public bool IncludeModdedItems { get; set; } = true;
    public bool IncludeModdedNPCs { get; set; } = true;
    public bool FestivalQuestsEnabled { get; set; } = true;
    public bool AnimalQuestsEnabled { get; set; } = true;
    public bool SecretGiftHintEnabled { get; set; } = true;

    /// When true, fishing quests can request any seasonal fish even if the player hasn't
    /// been to a location where it spawns. When false (default), the candidate pool is
    /// filtered to fish whose spawn locations the player has visited at least once.
    public bool FishingIgnoresVisitedLocations { get; set; } = false;

    /// When true, the same NPC may give multiple different ItemDelivery / Fishing quests
    /// in the same day. When false (default), the pipeline enforces one quest per giver.
    public bool AllowDuplicateGiverPerDay { get; set; } = false;

    /// When true (default), quests that reward friendship to a specific NPC are skipped if
    /// the player is already at max hearts with that NPC. Quests that reward friendship to
    /// a different NPC than the giver still post normally.
    public bool SkipFriendshipQuestsAtMaxHeart { get; set; } = true;

    // ----- Friendship rewards (raw friendship points; 250 = 1 heart) -----
    public int FriendshipBasic { get; set; } = 30;
    public int FriendshipMid { get; set; } = 80;
    public int FriendshipIntermediate { get; set; } = 125;
    public int FriendshipLarge { get; set; } = 250;
    public int FriendshipMultiSmall { get; set; } = 30;
    public int FriendshipMultiHeart { get; set; } = 250;

    // ----- Gold reward bases -----
    public int GoldBeginnerBase { get; set; } = 100;
    public int GoldBasicBase { get; set; } = 300;
    public int GoldIntermediateBase { get; set; } = 500;
    public int GoldAdvancedBase { get; set; } = 1000;
    public int GoldExpertBase { get; set; } = 3000;

    // ----- Reward multipliers vs item sell price -----
    public float RewardMultiplierBelowSell { get; set; } = 0.8f;
    public float RewardMultiplierAboveSell { get; set; } = 1.05f;
    public float RewardMultiplierFishPremium { get; set; } = 1.15f;

    // ----- Shop discounts -----
    public int ShopDiscountPercent { get; set; } = 50;
    public int ShopDiscountDurationDays { get; set; } = 2;
    public int SeedShopDiscountPercent { get; set; } = 20;
    public int SeedShopDiscountDurationDays { get; set; } = 3;

    // ----- Deadlines (in-game days) -----
    public int DeadlineShort { get; set; } = 2;
    public int DeadlineMedium { get; set; } = 5;
    public int DeadlineLong { get; set; } = 7;
    public int DeadlineExtended { get; set; } = 14;
    public int DeadlineNone { get; set; } = 999;

    // ----- Quantity tunables -----
    public int FishHaulMediumQty { get; set; } = 15;
    public int FishHaulLargeQty { get; set; } = 30;
    public int FestivalFishQty { get; set; } = 5;
    public int CropMassiveQty { get; set; } = 50;
    public int HaySupplyBaseQty { get; set; } = 10;

    // ----- Skull Cavern depth cap for Deep Dive quest -----
    public int SkullCavernMaxLevel { get; set; } = 100;
}
