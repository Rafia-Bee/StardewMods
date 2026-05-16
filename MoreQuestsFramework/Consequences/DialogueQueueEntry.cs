namespace MoreQuestsFramework.Consequences;

// Lines are pre-resolved (engine baked the i18n lookup at fire time) so the watcher
// only needs raw strings. EarliestFireDay lets Tier 3 chains spread across days.
public sealed class DialogueQueueEntry
{
    public string NpcName { get; set; } = "";
    public string Line { get; set; } = "";
    public int FriendshipDelta { get; set; }
    public int EarliestFireDay { get; set; }

    // Stardew Dialogue token: $h happy, $s sad, $a angry, $l love, $u unique.
    public string Portrait { get; set; } = "";
}
