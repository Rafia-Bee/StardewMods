using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Read-only snapshot of a quest currently posted on the vanilla quest board (the
// help-wanted billboard by Pierre's). Mirrors CustomBoardSlotInfo. A slot drops off
// this list the moment the player accepts it, so the list is exactly the quests
// still waiting to be accepted today.
public sealed class DailyBoardSlotInfo
{
    public string SyncId { get; }
    public Quest Quest { get; }
    public string DefinitionId { get; }
    public string OwnerUniqueId { get; }
    public bool Accepted { get; }

    public DailyBoardSlotInfo(string syncId, Quest quest, string definitionId, string ownerUniqueId, bool accepted)
    {
        SyncId = syncId;
        Quest = quest;
        DefinitionId = definitionId;
        OwnerUniqueId = ownerUniqueId;
        Accepted = accepted;
    }
}
