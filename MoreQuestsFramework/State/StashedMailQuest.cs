using System.Collections.Generic;

namespace MoreQuestsFramework.State;

/// Serializable mirror of a pending mail-quest posting. Persisted in
/// `FrameworkState.PendingMailDeliveries` so a mail letter sitting in the
/// player's mailbox at save time still resolves correctly on reload — the
/// `%item quest <mailKey> 1 %%` token in the letter body looks up its prepared
/// Quest by mailKey, and our Harmony prefix on `Quest.getQuestFromId` returns
/// the subclass we built from these fields.
///
/// Only non-PreBuilt postings are persistable through this path (custom Quest
/// subclasses with their own NetFields can't be reconstructed from a flat DTO
/// alone). Phase 6 mail quests all flow through `QuestFactory.Build`, which
/// returns the framework's own `MoreQuestsItemDeliveryQuest` /
/// `MoreQuestsFishingQuest` / `SlayMonsterQuest` — none rely on PreBuilt.
public sealed class StashedMailQuest
{
    public string MailKey { get; set; } = "";
    public string OwnerUniqueId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string MailBody { get; set; } = "";

    /// `BoardQuestType` enum value as int so JSON survives renames.
    public int QuestType { get; set; }
    public int Category { get; set; }
    public int Tier { get; set; }
    public string QuestGiver { get; set; } = "";
    public string ObjectiveItemId { get; set; } = "";
    public string ObjectiveItemName { get; set; } = "";
    /// OR-alternative ids accepted in place of `ObjectiveItemId`. Round-trips so a mail
    /// letter for "Submarine Fuel — battery OR coal" still resolves correctly on reload.
    public List<string> AlternativeObjectiveItemIds { get; set; } = new();
    public int ObjectiveQuantity { get; set; } = 1;
    public string? TargetMonster { get; set; }
    public int DeadlineDays { get; set; } = 5;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public string TargetMessage { get; set; } = "";

    /// Each reward is encoded via `RewardCodec.Encode` so the persisted record
    /// stays text-only — no polymorphic serializer required.
    public List<string> EncodedRewards { get; set; } = new();
}
