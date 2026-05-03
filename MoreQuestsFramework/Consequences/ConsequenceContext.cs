using System.Collections.Generic;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.State;
using StardewModdingAPI;

namespace MoreQuestsFramework.Consequences;

/// All the state a handler needs to enact a consequence. Constructed by `ConsequenceEngine`
/// at fire time so handlers don't reach for global statics.
public sealed class ConsequenceContext
{
    public ConsequenceSpec Spec { get; }
    public MoreQuestsFrameworkConfig Config { get; }
    public GiftTastesScanner GiftTastes { get; }
    public FrameworkState State { get; }
    public IMonitor Monitor { get; }

    /// NPCs the engine has resolved as "loved" the subject (Tier 1 positive branch).
    /// Empty for tiers that don't use the GiftTastes branch.
    public IReadOnlyList<string> LovedBy { get; }

    /// NPCs resolved as "hated" the subject (Tier 1/2 negative branch). For `Source =
    /// Static`, these are the spec's `Targets[]` verbatim — the static-targets list is
    /// always treated as "hated/affected" since static authors only specify negative
    /// reactors today (no positive static-target use-case in the CSV).
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

    /// Helper used by every tier handler — applies an immediate friendship delta to an
    /// NPC, skipping NPCs the player hasn't met (no entry in `friendshipData` ⇒ vanilla
    /// would silently no-op the change anyway, but checking up-front keeps the trace
    /// log honest about who actually got hit).
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

    /// Append one queue entry. Same-day pops use `earliestFireDay = 0`. `portrait` is
    /// a Stardew dialogue token (`$h`, `$a`, `$s`, `$l`, `$u`) prepended to the line
    /// before drawing — empty string means no portrait override.
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
