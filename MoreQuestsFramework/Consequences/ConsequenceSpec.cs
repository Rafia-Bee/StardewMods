using System.Collections.Generic;

namespace MoreQuestsFramework.Consequences;

/// Tiered post-completion fallout for a quest. Plan §2.5.
///
/// - `Tier0` — no consequence. Most quests.
/// - `Tier1` — comment-tier. Loved-comment + small positive friendship to NPCs who love
///   `Subject`; hated-comment + small negative friendship to NPCs who hate it.
/// - `Tier2` — small-loss. Multi-NPC negative reaction (e.g. saloon Weekly Special Complex).
/// - `Tier3` — significant. Multi-day chained dialogue + large friendship loss to a fixed
///   set of ecology NPCs (Seafood Night → Demetrius / Linus / mod analogues).
/// - `Special` — gold loss. Player loses gold on quest completion.
public enum ConsequenceTier
{
    Tier0,
    Tier1,
    Tier2,
    Tier3,
    Special
}

/// How the consequence engine resolves which NPCs are affected.
///
/// - `GiftTastes` — scan `Data/NPCGiftTastes` for NPCs whose loved/hated list mentions
///   `Subject`. Used by saloon dish + crop / fish category quests.
/// - `Static` — apply to a fixed `Targets[]` list (configured by the caller). Used by
///   Tier 3 ecology consequences and any tier where the affected NPCs aren't taste-driven.
public enum ConsequenceSource
{
    GiftTastes,
    Static
}

/// One consequence block attached to a `QuestPosting`. The engine fires it on
/// `questComplete()` for every `IRewardedQuest` and `AdventureQuest` that carries one.
///
/// Dialogue lines are passed in pre-resolved (the generator owns the translation
/// helper) — the engine snapshot-encodes lines into the persistent queue at fire time
/// so the watcher only needs raw strings to pop. That keeps the queue self-contained
/// across save/reload without re-resolving translations.
public sealed class ConsequenceSpec
{
    public ConsequenceTier Tier { get; set; } = ConsequenceTier.Tier0;
    public ConsequenceSource Source { get; set; } = ConsequenceSource.GiftTastes;

    /// Item id (qualified or bare) the consequence is "about" — the dish, the crop, the
    /// fish category. Required for `Source = GiftTastes`. Ignored for `Source = Static`.
    public string Subject { get; set; } = "";

    /// NPC names the consequence applies to when `Source = Static`. For `GiftTastes` this
    /// list is appended to the resolved set (so authors can hard-code an extra NPC even
    /// when scanning gift tastes — useful when the quest dispatcher's NPC isn't covered
    /// by the taste scan).
    public List<string> Targets { get; set; } = new();

    /// Override gold delta for `Special` tier. Negative = player loses; positive would
    /// reward (not currently used). Ignored for other tiers.
    public int GoldDelta { get; set; }

    /// Optional override of the per-NPC friendship delta. Zero = use the tier's default
    /// (`+/- FriendshipBasic` for Tier 1, `-FriendshipBasic..-FriendshipMid` for Tier 2,
    /// `-FriendshipLarge` for Tier 3).
    public int FriendshipOverride { get; set; }

    /// Number of days a Tier 3 chained dialogue runs. One line per day for `ChainDays`
    /// consecutive days starting the day after completion. Defaults to 3 if zero.
    /// Ignored for other tiers.
    public int ChainDays { get; set; }

    /// Pre-resolved positive (loved) dialogue line. Used for the Tier 1 loved branch.
    /// Empty = no dialogue, only the friendship delta is applied.
    public string LovedLine { get; set; } = "";

    /// Pre-resolved negative (hated) dialogue line. Used for the Tier 1 hated branch
    /// and Tier 2's single-day reaction. Empty = no dialogue, only the friendship delta.
    public string HatedLine { get; set; } = "";

    /// Pre-resolved Tier 3 chained lines, one per day in order. The engine pops one per
    /// day per NPC; the chain length is `min(Lines.Count, ChainDays)`. Empty = no
    /// dialogue, only the friendship delta on day 0.
    public List<string> ChainLines { get; set; } = new();
}
