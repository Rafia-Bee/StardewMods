using System.Collections.Generic;

namespace MoreQuestsFramework.Config;

public sealed class MoreQuestsFrameworkConfig
{
    public int QuestsPerDay { get; set; } = 3;

    // 1 = vanilla (two random orders). 2-5 paginates every eligible order in
    // Data/SpecialOrders (vanilla + every loaded mod's), 2 per page.
    public int SpecialOrdersBoardPages { get; set; } = 1;

    // Keys are definition IDs. 0 disables. Missing keys use DefaultWeight.
    public Dictionary<string, int> QuestWeights { get; set; } = new();

    public bool DifficultyScaling { get; set; } = true;

    // When false (default), the candidate pool is filtered to locations the player
    // has visited at least once.
    public bool FishingIgnoresVisitedLocations { get; set; } = false;

    public bool ForagingIgnoresVisitedLocations { get; set; } = false;

    public bool AllowDuplicateGiverPerDay { get; set; } = false;

    // Quests rewarding friendship to a *different* NPC than the giver still post.
    public bool SkipFriendshipQuestsAtMaxHeart { get; set; } = true;

    // Internal NPC names that should never be a board / special-order quest giver,
    // on top of the per-character heuristics in NpcDisplay.IsBoardEligible (child,
    // CanSocialize=false, PerfectionScore=false). Use this for friendable monsters
    // whose CharacterData is crafted like a real NPC (full perfection / slideshow
    // entries), so the heuristic can't tell them apart from a real human. Defaults
    // cover the friendable monsters / creatures that ship with my installed packs.
    public List<string> IneligibleGivers { get; set; } = new()
    {
        "Krobus",
        "Leximonster",
        "SenS",
    };

    // After this many days, a queued consequence dialogue line is dropped on DayStarted.
    public int ConsequenceGraceDays { get; set; } = 7;

    // Raw friendship points; 250 = 1 heart.
    public int FriendshipBasic { get; set; } = 30;
    public int FriendshipMid { get; set; } = 80;
    public int FriendshipIntermediate { get; set; } = 125;
    public int FriendshipLarge { get; set; } = 250;
    public int FriendshipMultiSmall { get; set; } = 30;
    public int FriendshipMultiHeart { get; set; } = 250;

    public int GoldBeginnerBase { get; set; } = 100;
    public int GoldBasicBase { get; set; } = 300;
    public int GoldIntermediateBase { get; set; } = 500;
    public int GoldAdvancedBase { get; set; } = 1000;
    public int GoldExpertBase { get; set; } = 3000;

    public float RewardMultiplierBelowSell { get; set; } = 0.8f;
    public float RewardMultiplierAboveSell { get; set; } = 1.05f;
    public float RewardMultiplierFishPremium { get; set; } = 1.15f;

    // In-game days.
    public int DeadlineShort { get; set; } = 2;
    public int DeadlineMedium { get; set; } = 5;
    public int DeadlineLong { get; set; } = 7;
    public int DeadlineExtended { get; set; } = 14;
    public int DeadlineNone { get; set; } = 999;
}
