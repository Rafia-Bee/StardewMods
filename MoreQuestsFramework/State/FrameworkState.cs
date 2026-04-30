using System.Collections.Generic;

namespace MoreQuestsFramework.State;

/// Per-save persistent state owned by the framework. Stored under
/// `Helper.Data.WriteSaveData("MoreQuestsFrameworkState", state)` per plan.md §9.
///
/// All collections are keyed by the registered quest definition id (the JSON
/// `Name` verbatim — auto-prefixing by ownerUniqueId is deferred to Phase 10).
/// Empty/zero values are valid defaults, so a save written before any Phase 6
/// trigger fires deserialises into a clean state.
public sealed class FrameworkState
{
    /// Schema version. Bumped when the shape changes in an incompatible way so
    /// future migrations can branch on it.
    public int Schema { get; set; } = 1;

    /// Absolute `Game1.Date.TotalDays` of the most recent fire for each definition.
    /// Used by `Periodic`, `DateLocked` (yearly), and post-fire bookkeeping.
    public Dictionary<string, int> LastFiredDay { get; set; } = new();

    /// True for every definition whose OneShot `When` predicate has fired this save.
    /// Once set, a definition never fires again on this save.
    public Dictionary<string, bool> OneShotFired { get; set; } = new();

    /// Snapshot of farm-building types as of the last DayStarted. Compared on the
    /// next DayStarted to detect newly built buildings without polling.
    public List<string> LastSeenBuildings { get; set; } = new();

    /// Snapshot of `Game1.player.mailReceived` flags from the last DayStarted. Diffed
    /// to detect newly received mail flags.
    public List<string> LastSeenMailFlags { get; set; } = new();

    /// Definition id → `Game1.Date.TotalDays` on which a deferred (DayDelay) trigger
    /// should actually fire. Cleared once the day arrives.
    public Dictionary<string, int> ScheduledFireDay { get; set; } = new();

    /// Definition id → target NPC name. The DialogueWatcher pushes the quest into
    /// the journal the next time the player speaks with that NPC, then removes the
    /// entry. Survives save/load so a quest queued just before bedtime still fires.
    public Dictionary<string, string> PendingDialogueQuests { get; set; } = new();

    /// Mail-delivered quests waiting for the player to open the letter. Each entry
    /// carries the full posting + body; the framework rebuilds the Quest object via
    /// `QuestFactory` and re-injects the body into `Data/mail` at SaveLoaded so a
    /// letter sitting unread in the mailbox across save/load still resolves to the
    /// same quest.
    public List<StashedMailQuest> PendingMailDeliveries { get; set; } = new();
}
