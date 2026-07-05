using System;
using StardewValley;

namespace MoreQuestsFramework.Consequences;

internal sealed class Tier0Handler : IConsequenceHandler
{
    public void Apply(ConsequenceContext _) { }
}

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

// Per-day friendship: FriendshipPerDay verbatim if non-zero, else
// FriendshipOverride/ChainDays, else -FriendshipLarge/ChainDays.
internal sealed class Tier3Handler : IConsequenceHandler
{
    // Used when a spec doesn't set its own ChainDays: spread the loss over three days.
    private const int DefaultChainDays = 3;

    public void Apply(ConsequenceContext ctx)
    {
        int chainDays = ctx.Spec.ChainDays > 0 ? ctx.Spec.ChainDays : DefaultChainDays;
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

        // Walk loved-by too in case a future Tier 3 author wants a positive chain.
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

internal sealed class SpecialTierHandler : IConsequenceHandler
{
    public void Apply(ConsequenceContext ctx)
    {
        int loss = Math.Abs(ctx.Spec.GoldDelta);
        if (loss > 0 && Game1.player != null)
            Game1.player.Money = Math.Max(0, Game1.player.Money - loss);

        // Defaults to zero so gold-loss tier doesn't double-tax friendship by default.
        int friendshipDelta = ctx.Spec.FriendshipOverride;
        foreach (var npc in ctx.HatedBy)
            ctx.EnqueueLine(npc, ctx.Spec.HatedLine, friendshipDelta, earliestFireDay: 0, portrait: "$a");
    }
}
