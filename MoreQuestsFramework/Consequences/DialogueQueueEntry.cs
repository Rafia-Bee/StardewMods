namespace MoreQuestsFramework.Consequences;

/// One pending consequence dialogue line, waiting to be popped the next time the player
/// chats with `NpcName` on or after `EarliestFireDay`. Persisted in
/// `FrameworkState.PendingConsequenceLines` so a queue entry survives save/reload.
///
/// Lines are stored pre-resolved (the engine baked the i18n lookup at fire time) so the
/// watcher only needs raw strings to pop. Friendship deltas ride alongside so a
/// Tier 3 chain can keep nudging the relationship one bucket per day without holding
/// onto the original spec.
///
/// `EarliestFireDay` lets Tier 3 chains spread across consecutive days — entry N in a
/// chain stamps `today + N` so the watcher won't pop it before that day. Same-day
/// entries (Tier 1/2) leave it at zero.
public sealed class DialogueQueueEntry
{
    public string NpcName { get; set; } = "";
    public string Line { get; set; } = "";
    public int FriendshipDelta { get; set; }
    public int EarliestFireDay { get; set; }

    /// Portrait expression token to prepend before the line. Stardew's `Dialogue`
    /// parser recognises `$h` (happy), `$s` (sad), `$a` (angry), `$l` (love), `$u`
    /// (unique). NPCs without a custom portrait for the chosen expression fall back
    /// to neutral, so this is safe across modded NPCs too. Empty = no override.
    public string Portrait { get; set; } = "";
}
