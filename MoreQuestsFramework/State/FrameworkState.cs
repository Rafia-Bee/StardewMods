using System;
using System.Collections.Generic;
using MoreQuestsFramework.Consequences;

namespace MoreQuestsFramework.State;

// Per-save persistent state. All dict keys are the JSON-Name verbatim of the quest def.
internal sealed class FrameworkState
{
    // Bumped on incompatible shape changes so StateStore.Load can branch and migrate.
    // v2 added the notice collections below; the bump is purely additive (a v1 save has no
    // notice fields, so they deserialize as empty), so the migration is the existing no-op.
    public const int CurrentSchema = 2;

    public int Schema { get; set; } = CurrentSchema;

    public Dictionary<string, int> LastFiredDay { get; set; } = new();

    public Dictionary<string, int> AntiRepetitionLastPostedDay { get; set; } = new();

    public Dictionary<string, bool> OneShotFired { get; set; } = new();

    public List<string> LastSeenBuildings { get; set; } = new();

    public List<string> LastSeenMailFlags { get; set; } = new();

    // Deferred (DayDelay) triggers. Cleared once the day arrives.
    public Dictionary<string, int> ScheduledFireDay { get; set; } = new();

    public Dictionary<string, string> PendingDialogueQuests { get; set; } = new();

    public List<StashedMailQuest> PendingMailDeliveries { get; set; } = new();

    public List<EmittedSpecialOrder> EmittedSpecialOrders { get; set; } = new();

    // Dedupes FrameworkRewards grants across save/reload.
    public List<string> FrameworkRewardsGranted { get; set; } = new();

    public List<ActiveShopDiscount> ActiveShopDiscounts { get; set; } = new();

    public List<ActiveAnimalPurchaseDiscount> ActiveAnimalPurchaseDiscounts { get; set; } = new();

    public List<DialogueQueueEntry> PendingConsequenceLines { get; set; } = new();

    // Per-NPC per-day clamp so a player who skips days doesn't get every queued chain
    // line back-to-back on the next chat.
    public Dictionary<string, int> LastConsequencePoppedDay { get; set; } = new();

    public List<ActiveFestivalBias> ActiveFestivalBiases { get; set; } = new();

    public List<ActiveFairStarTokens> ActiveFairStarTokens { get; set; } = new();

    // Bulletin notices. Keys are the namespaced {owner}/{Name} of the notice def.
    // One-shot notices: a notice id lands here the day it's first shown and is then excluded
    // from every future draw.
    public List<string> SeenNotices { get; set; } = new();

    // Per-notice cooldown clock: the TotalDays a notice was last shown. A recurring notice
    // with CooldownDays > 0 waits that long before it can be drawn again.
    public Dictionary<string, int> NoticeLastPostedDay { get; set; } = new();

    // Reserved for the Phase 2 pinned / weekly-persistent notices (a notice that stays on the
    // board for N days or the rest of the week without re-rolling). Unused today; defined now
    // so adding the feature needs no second schema bump.
    public List<PostedNotice> PostedNotices { get; set; } = new();

    // Drops entries keyed by a defId that no longer maps to a registered quest, so
    // uninstalling a consumer mod doesn't leave its keys lingering in the save forever.
    // Returns the total number of entries removed across all collections.
    public int PruneDeadDefIds(IReadOnlyCollection<string> registeredIds)
    {
        var live = new HashSet<string>(registeredIds, StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        removed += DropMissing(LastFiredDay, live);
        removed += DropMissing(AntiRepetitionLastPostedDay, live);
        removed += DropMissing(OneShotFired, live);
        removed += DropMissing(ScheduledFireDay, live);
        removed += DropMissing(PendingDialogueQuests, live);
        return removed;
    }

    // Drops saved notice flags whose {owner}/{Name} no longer maps to a registered notice, so
    // uninstalling a notice pack doesn't leave its seen/cooldown entries in the save forever.
    public int PruneDeadNoticeIds(IReadOnlyCollection<string> registeredNoticeIds)
    {
        var live = new HashSet<string>(registeredNoticeIds, StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        if (SeenNotices.Count > 0)
        {
            int before = SeenNotices.Count;
            SeenNotices.RemoveAll(id => !live.Contains(id));
            removed += before - SeenNotices.Count;
        }
        removed += DropMissing(NoticeLastPostedDay, live);
        if (PostedNotices.Count > 0)
        {
            int before = PostedNotices.Count;
            PostedNotices.RemoveAll(p => !live.Contains(p.DefinitionId));
            removed += before - PostedNotices.Count;
        }
        return removed;
    }

    private static int DropMissing<TValue>(Dictionary<string, TValue> dict, HashSet<string> live)
    {
        if (dict.Count == 0)
            return 0;
        List<string>? dead = null;
        foreach (var key in dict.Keys)
        {
            if (!live.Contains(key))
                (dead ??= new List<string>()).Add(key);
        }
        if (dead == null)
            return 0;
        foreach (var key in dead)
            dict.Remove(key);
        return dead.Count;
    }
}

// Phase 2 placeholder for a notice pinned to the board for a span of days (a fixed PinDays
// window or the rest of the week). FirstPostedDay anchors the span; the notice re-appears
// without re-rolling until ExpiresAfterDay, then a daily sweep drops the entry.
internal sealed class PostedNotice
{
    // Namespaced {owner}/{Name} of the notice def.
    public string DefinitionId { get; set; } = "";
    public int FirstPostedDay { get; set; }
    public int ExpiresAfterDay { get; set; }
}

internal sealed class ActiveFestivalBias
{
    // Stringly-typed so the enum can move without breaking save compat.
    public string Festival { get; set; } = "";

    // Luau: tier-bump steps. Vanilla reaction tiers are 0=Disgusting through 4=Loved
    // it (5 is "missing ingredients", 6 is the Mayor's Shorts gag), so the patch caps
    // the boosted tier at 4. Fair: added directly to grangeScore.
    public int Magnitude { get; set; }

    public int ExpiresAfterDay { get; set; }
}

internal sealed class ActiveFairStarTokens
{
    public int Amount { get; set; }
    public int ExpiresAfterDay { get; set; }
}

internal sealed class ActiveShopDiscount
{
    public string ShopId { get; set; } = "";
    public int PercentOff { get; set; }
    public int ExpiresAfterDay { get; set; }
    // Empty = discount the entire shop.
    public List<string> AppliesTo { get; set; } = new();
    // >0 force-adds missing AppliesTo items as temporary entries.
    public int GuaranteedStock { get; set; }
}

internal sealed class ActiveAnimalPurchaseDiscount
{
    public int PercentOff { get; set; }
    public int ExpiresAfterDay { get; set; }
}

// Spec is the framework-neutral shape; the writer rebuilds the vanilla SpecialOrderData
// instance per edit pass (avoids SMAPI's AsDictionary cast issue with pre-serialised JSON).
internal sealed class EmittedSpecialOrder
{
    public string OrderId { get; set; } = "";

    public int EmittedDay { get; set; }

    public int ExpiresAfterDay { get; set; }

    public string OwnerUniqueId { get; set; } = "";

    public string DefinitionId { get; set; } = "";

    public Pipeline.SpecialOrderSpec Spec { get; set; } = new();
}
