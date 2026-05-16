using System.Collections.Generic;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.State;
using StardewModdingAPI;

namespace MoreQuestsFramework.Consequences;

public sealed class ConsequenceContext
{
    public ConsequenceSpec Spec { get; }
    public MoreQuestsFrameworkConfig Config { get; }
    public GiftTastesScanner GiftTastes { get; }
    public FrameworkState State { get; }
    public IMonitor Monitor { get; }

    public IReadOnlyList<string> LovedBy { get; }

    // For Source=Static, these are Targets[] verbatim (static is always treated as
    // the affected/hated side; no positive static-target use case today).
    public IReadOnlyList<string> HatedBy { get; }

    public ConsequenceContext(
        ConsequenceSpec spec,
        MoreQuestsFrameworkConfig config,
        GiftTastesScanner giftTastes,
        FrameworkState state,
        IMonitor monitor,
        IReadOnlyList<string> lovedBy,
        IReadOnlyList<string> hatedBy)
    {
        Spec = spec;
        Config = config;
        GiftTastes = giftTastes;
        State = state;
        Monitor = monitor;
        LovedBy = lovedBy;
        HatedBy = hatedBy;
    }

    public bool ChangeFriendship(string npcName, int delta)
    {
        if (string.IsNullOrEmpty(npcName) || delta == 0)
            return false;
        var npc = StardewValley.Game1.getCharacterFromName(npcName);
        if (npc == null)
            return false;
        StardewValley.Game1.player.changeFriendship(delta, npc);
        return true;
    }

    // Same-day pops use earliestFireDay=0. Portrait is a Stardew dialogue token
    // ($h/$a/$s/$l/$u), or empty for no override.
    public void EnqueueLine(string npcName, string line, int friendshipDelta, int earliestFireDay, string portrait = "")
    {
        if (string.IsNullOrEmpty(npcName))
            return;
        State.PendingConsequenceLines.Add(new DialogueQueueEntry
        {
            NpcName = npcName,
            Line = line ?? string.Empty,
            FriendshipDelta = friendshipDelta,
            EarliestFireDay = earliestFireDay,
            Portrait = portrait ?? string.Empty
        });
    }
}
