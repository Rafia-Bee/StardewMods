using System.Collections.Generic;

namespace MoreQuestsFramework.Config;

/// Engine-wide tunables. Cross-cutting settings that affect the daily-board pipeline,
/// reward sizing, and deadlines. Per-quest content settings live in the consuming
/// content mod's own config (e.g. RafiaBee.MoreQuests).
public sealed class MoreQuestsFrameworkConfig
{
    // ----- Quest board -----
    public int QuestsPerDay { get; set; } = 3;

    /// How many pages of orders the SpecialOrders board displays. 1 = vanilla behaviour
    /// (the two random orders vanilla picked at the weekly refresh). 2 or 3 enables a
    /// paginated view that surfaces every eligible order in `Data/SpecialOrders` (vanilla
    /// + every loaded mod's), 2 per page, with prev/next arrows. Capped at 3 so heavily
    /// modded saves don't flood the board.
    public int SpecialOrdersBoardPages { get; set; } = 1;

    /// Per-definition selection weight for the daily board. Keys are definition IDs
    /// (e.g. "Vanilla.ItemDelivery", "Farming.BasicCropDelivery"). Values are relative
    /// weights; 0 disables the definition. Missing keys fall back to each definition's
    /// declared DefaultWeight.
    public Dictionary<string, int> QuestWeights { get; set; } = new();

    // ----- Master toggles -----
    public bool DifficultyScaling { get; set; } = true;

    /// When true, fishing quests can request any seasonal fish even if the player hasn't
    /// been to a location where it spawns. When false (default), the candidate pool is
    /// filtered to fish whose spawn locations the player has visited at least once.
    public bool FishingIgnoresVisitedLocations { get; set; } = false;

    /// When true, foraging-flavoured quests (seasonal forage, rare-forage hunt, Caroline's
    /// off-season tea, the Winter Star feast) can request items spawning in locations the
    /// player has not visited. When false (default), the candidate pool is filtered to
    /// forage whose Data/Locations spawn entries match a visited location. Falls back to
    /// the full pool on a fresh save (player has only been to the farm).
    public bool ForagingIgnoresVisitedLocations { get; set; } = false;

    /// When true, the same NPC may give multiple different ItemDelivery / Fishing quests
    /// in the same day. When false (default), the pipeline enforces one quest per giver.
    public bool AllowDuplicateGiverPerDay { get; set; } = false;

    /// When true (default), quests that reward friendship to a specific NPC are skipped if
    /// the player is already at max hearts with that NPC. Quests that reward friendship to
    /// a different NPC than the giver still post normally.
    public bool SkipFriendshipQuestsAtMaxHeart { get; set; } = true;

    /// Days past a consequence dialogue entry's `EarliestFireDay` after which it gets
    /// silently dropped on `DayStarted`. Stops chained / queued reactions from sitting
    /// in the queue indefinitely on saves where the player ducks the NPC for weeks —
    /// an NPC isn't going to bring up an overfishing complaint a year after the fact.
    public int ConsequenceGraceDays { get; set; } = 7;

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

    // ----- Deadlines (in-game days) -----
    public int DeadlineShort { get; set; } = 2;
    public int DeadlineMedium { get; set; } = 5;
    public int DeadlineLong { get; set; } = 7;
    public int DeadlineExtended { get; set; } = 14;
    public int DeadlineNone { get; set; } = 999;
}
