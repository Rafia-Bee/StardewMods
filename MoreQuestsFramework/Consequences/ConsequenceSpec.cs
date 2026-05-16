using System.Collections.Generic;

namespace MoreQuestsFramework.Consequences;

// Tier0: none. Tier1: small comment + +/-FriendshipBasic. Tier2: multi-NPC small
// loss. Tier3: multi-day chain + large friendship loss to a fixed ecology set.
// Special: gold loss.
public enum ConsequenceTier
{
    Tier0,
    Tier1,
    Tier2,
    Tier3,
    Special
}

// GiftTastes: scan Data/NPCGiftTastes for NPCs with Subject in loved/hated list.
// Static: use Targets[] verbatim.
public enum ConsequenceSource
{
    GiftTastes,
    Static
}

public sealed class ConsequenceSpec
{
    public ConsequenceTier Tier { get; set; } = ConsequenceTier.Tier0;
    public ConsequenceSource Source { get; set; } = ConsequenceSource.GiftTastes;

    // Item id (qualified or bare) for GiftTastes lookup. Ignored when Source=Static.
    public string Subject { get; set; } = "";

    // For Static, the affected set. For GiftTastes, appended to the resolved set.
    public List<string> Targets { get; set; } = new();

    // Special tier only. Negative = player loses gold.
    public int GoldDelta { get; set; }

    // Zero = tier default. For Tier 3 this is TOTAL loss spread across ChainDays
    // (use FriendshipPerDay instead for a per-day value).
    public int FriendshipOverride { get; set; }

    // Tier 3 only. When non-zero, applied verbatim per chain day (no division).
    public int FriendshipPerDay { get; set; }

    // Tier 3 only. Defaults to 3 when zero.
    public int ChainDays { get; set; }

    public string LovedLine { get; set; } = "";

    public string HatedLine { get; set; } = "";

    // Tier 3 only. One line per chain day, in order.
    public List<string> ChainLines { get; set; } = new();
}
