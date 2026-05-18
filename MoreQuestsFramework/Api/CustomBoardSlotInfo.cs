using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Read-only snapshot of a quest currently posted on a custom board. Consumer-mod
// debug menus and quest browsers can poll this between board refreshes.
public sealed class CustomBoardSlotInfo
{
    public string SyncId { get; }
    public Quest Quest { get; }
    public string BoardOwnerUniqueId { get; }
    public string BoardName { get; }
    public string DefinitionId { get; }
    public string OwnerUniqueId { get; }
    public bool Accepted { get; }

    public CustomBoardSlotInfo(string syncId, Quest quest, string boardOwnerUniqueId, string boardName, string definitionId, string ownerUniqueId, bool accepted)
    {
        SyncId = syncId;
        Quest = quest;
        BoardOwnerUniqueId = boardOwnerUniqueId;
        BoardName = boardName;
        DefinitionId = definitionId;
        OwnerUniqueId = ownerUniqueId;
        Accepted = accepted;
    }
}
