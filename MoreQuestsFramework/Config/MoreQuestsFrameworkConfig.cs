using System.Collections.Generic;

namespace MoreQuestsFramework.Config;

public sealed class MoreQuestsFrameworkConfig
{
    public int QuestsPerDay { get; set; } = 3;

    // 1 = vanilla (two random orders). 2-5 paginates every eligible order in
    // Data/SpecialOrders (vanilla + every loaded mod's), 2 per page.
    public int SpecialOrdersBoardPages { get; set; } = 1;

    // Daily board note layout. Notes sit on an auto-sized grid; BoardNoteSpacing is the
    // average gap in pixels between note papers (0 = touching, negative = overlapping), and
    // each gap is jittered a little so the layout isn't perfectly uniform. BoardMaxNoteSize
    // caps how big a single note can get when only a few are posted (it never grows past what
    // fits the board either way).
    public int BoardNoteSpacing { get; set; } = 14;
    public int BoardMaxNoteSize { get; set; } = 256;

    // Per-custom-board pin count chosen by the player. Keys are the board's "{owner}/{name}"
    // id. A board not listed here uses its authored PoolSize. The stored value is clamped to
    // the board's PoolSizeMin..PoolSizeMax at read time, so out-of-range hand edits are safe.
    public Dictionary<string, int> CustomBoardPoolSize { get; set; } = new();

    // Per-custom-board notice (bulletin) pin count chosen by the player. Same "{owner}/{name}"
    // keys as CustomBoardPoolSize. A board not listed uses its authored NoticePoolSize. Clamped
    // to the board's NoticePoolSizeMin..Max at read time. May be 0 to hide notices.
    public Dictionary<string, int> CustomBoardNoticePoolSize { get; set; } = new();

    // Keys are definition IDs. 0 disables. Missing keys use DefaultWeight.
    public Dictionary<string, int> QuestWeights { get; set; } = new();

    // Per-mail-quest probability gate (0-100). Used by Mail-kind quest definitions
    // that want an extra chance roll on top of their trigger (e.g. Rainy Day Catch,
    // which only fires when tomorrow is forecast rain, gated further by this value).
    // Keys are definition IDs. Missing keys default to 100 (always fire when the
    // trigger condition matches). 0 disables the quest entirely.
    public Dictionary<string, int> MailQuestChancePercent { get; set; } = new();

    public bool DifficultyScaling { get; set; } = false;

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
    // and animal NPCs whose CharacterData is crafted like a real NPC (full perfection
    // / slideshow entries), so the heuristic can't tell them apart from a real human.
    // Defaults cover the friendable monsters / creatures that ship with my installed
    // packs. Torts is an RSV tortoise with Age=Child, so he was getting picked for
    // the child-only Feed Wild Critters quest. The Dwarf is a real villager (Age=Adult,
    // PerfectionScore=true) so the heuristics let him through, but he lives in the mines
    // and speaks Dwarvish, so a town help-wanted post from him reads wrong.
    public List<string> IneligibleGivers { get; set; } = new()
    {
        "Krobus",
        "Leximonster",
        "SenS",
        "Torts",
        "Dwarf",
    };

    // When a daily-board posting has a hardcoded giver who's on the exclusion list,
    // on (default) redirects it to mail so the player still receives the quest. Off
    // drops it entirely. Either path also logs a WARN line so mod authors notice.
    // Special-order postings always drop because vanilla owns that UI.
    public bool MailFallbackForExcludedGivers { get; set; } = true;

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

    // Shared cooldown buckets, in in-game days. A quest opts into a bucket with
    // Trigger.CooldownTier ("Short" / "Medium" / "Long") in quests.json and waits that many
    // days before it can re-roll onto the board. These seed the CooldownTiers asset, which CP
    // packs can still edit to add their own tier names on top.
    public int CooldownShortDays { get; set; } = 2;
    public int CooldownMediumDays { get; set; } = 7;
    public int CooldownLongDays { get; set; } = 14;

    // When on, internal diagnostic logs are written at Trace level. Off in release builds
    // by default so the SMAPI log stays quiet; flip on if you're chasing a bug.
    public bool DebugLogging { get; set; } = false;
}
