using System;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

/// No-op handler for Tier 0. Registered so the engine never has to special-case the
/// "no consequence" tier in its dispatch table.
internal sealed class Tier0Handler : IConsequenceHandler
{
    public void Apply(ConsequenceContext _) { }
}

/// Tier 1, comment-tier. Loved-NPCs get a positive line + `+FriendshipBasic`. Hated
/// NPCs get a negative line + `-FriendshipBasic`. Lines are pre-resolved on the spec
/// (`LovedLine` / `HatedLine`); when empty, only the friendship delta lands. Single-day,
/// pops the next time the player chats with the affected NPC.
internal sealed class Tier1Handler : IConsequenceHandler
{
    public void Apply(ConsequenceContext ctx)
    {
        int basic = ctx.Spec.FriendshipOverride != 0 ? ctx.Spec.FriendshipOverride : ctx.Config.FriendshipBasic;

        foreach (var npc in ctx.LovedBy)
            ctx.EnqueueLine(npc, ctx.Spec.LovedLine, +basic, earliestFireDay: 0, portrait: "$h");

        foreach (var npc in ctx.HatedBy)
            ctx.EnqueueLine(npc, ctx.Spec.HatedLine, -basic, earliestFireDay: 0, portrait: "$a");
    }
}

/// Tier 2, small loss. Multi-NPC negative reaction with a slightly bigger friendship
/// hit (defaults to halfway between `FriendshipBasic` and `FriendshipMid`). Loved NPCs
/// also get a positive comment if the spec ships one (Weekly Special Complex's CSV row
/// shows mixed reactions on the same dish, Shane likes peppers, Haley hates them).
internal sealed class Tier2Handler : IConsequenceHandler
{
    public void Apply(ConsequenceContext ctx)
    {
        int positive = ctx.Spec.FriendshipOverride > 0 ? ctx.Spec.FriendshipOverride : ctx.Config.FriendshipBasic;
        int negative = ctx.Spec.FriendshipOverride < 0
            ? ctx.Spec.FriendshipOverride
            : -((ctx.Config.FriendshipBasic + ctx.Config.FriendshipMid) / 2);

        foreach (var npc in ctx.LovedBy)
            ctx.EnqueueLine(npc, ctx.Spec.LovedLine, +positive, earliestFireDay: 0, portrait: "$h");

        foreach (var npc in ctx.HatedBy)
            ctx.EnqueueLine(npc, ctx.Spec.HatedLine, negative, earliestFireDay: 0, portrait: "$a");
    }
}

/// Tier 3, significant. Multi-day chained dialogue. Friendship loss is applied per day
/// on each of `ChainDays` consecutive days, stamped with stepping `EarliestFireDay` so
/// the watcher only pops a line after each successive day starts. Lines come from
/// `Spec.ChainLines`; one line per day per NPC.
///
/// The per-day friendship delta is picked in this order:
/// 1. `Spec.FriendshipPerDay` if non-zero. Used verbatim per day (no division). Caller
///    is responsible for sign and magnitude. e.g. `FriendshipPerDay = -FriendshipMid`
///    with `ChainDays = 3` ⇒ -FriendshipMid each day, total = -3 * FriendshipMid.
/// 2. `Spec.FriendshipOverride / ChainDays` if `FriendshipOverride` is non-zero. Legacy
///    "total loss spread across the chain" semantics.
/// 3. `-FriendshipLarge / ChainDays` as the tier default. Same legacy semantics.
internal sealed class Tier3Handler : IConsequenceHandler
{
    public void Apply(ConsequenceContext ctx)
    {
        int chainDays = ctx.Spec.ChainDays > 0 ? ctx.Spec.ChainDays : 3;
        int perDay;
        if (ctx.Spec.FriendshipPerDay != 0)
        {
            perDay = ctx.Spec.FriendshipPerDay;
        }
        else
        {
            int totalLoss = ctx.Spec.FriendshipOverride != 0 ? ctx.Spec.FriendshipOverride : -ctx.Config.FriendshipLarge;
            perDay = totalLoss / chainDays;
        }
        int today = Game1.Date?.TotalDays ?? 0;
        var lines = ctx.Spec.ChainLines;

        // Static-source quests put their NPCs in `HatedBy`; loved-by stays empty for
        // ecology quests (Demetrius is never going to be happy you overfished). We still
        // walk loved-by in case a future Tier 3 author wants a positive-side chain.
        foreach (var npc in ctx.HatedBy)
            EnqueueChain(ctx, npc, perDay, chainDays, today, lines);
        foreach (var npc in ctx.LovedBy)
            EnqueueChain(ctx, npc, +Math.Abs(perDay), chainDays, today, lines);
    }

    private static void EnqueueChain(
        ConsequenceContext ctx,
        string npc,
        int perDayDelta,
        int chainDays,
        int today,
        System.Collections.Generic.List<string> lines)
    {
        string portrait = perDayDelta < 0 ? "$a" : "$h";
        for (int day = 0; day < chainDays; day++)
        {
            string line = day < lines.Count ? lines[day] : string.Empty;
            ctx.EnqueueLine(npc, line, perDayDelta, earliestFireDay: today + day, portrait: portrait);
        }
    }
}

/// Special, gold loss. Subtracts `Spec.GoldDelta` from the player's wallet and queues
/// a single line per affected NPC (HatedBy only, gold loss tiers are never positive
/// in the CSV today). `GoldDelta` is the absolute amount; the handler subtracts it.
internal sealed class SpecialTierHandler : IConsequenceHandler
{
    public void Apply(ConsequenceContext ctx)
    {
        int loss = Math.Abs(ctx.Spec.GoldDelta);
        if (loss > 0 && Game1.player != null)
            Game1.player.Money = Math.Max(0, Game1.player.Money - loss);

        // Optional dialogue per static target, used when the consequence has a
        // narrative explanation (e.g. "you owe a fine for X"). Friendship side is
        // controlled by FriendshipOverride; defaults to zero so gold-loss tier doesn't
        // double-tax relationships unless the author asks for it.
        int friendshipDelta = ctx.Spec.FriendshipOverride;
        foreach (var npc in ctx.HatedBy)
            ctx.EnqueueLine(npc, ctx.Spec.HatedLine, friendshipDelta, earliestFireDay: 0, portrait: "$a");
    }
}
