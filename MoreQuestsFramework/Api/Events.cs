using System;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

public sealed class QuestAcceptedArgs : EventArgs
{
    public Quest Quest { get; }
    public string OwnerUniqueId { get; }
    public string DefinitionId { get; }
    public QuestAcceptedArgs(Quest quest, string ownerUniqueId, string definitionId)
    {
        Quest = quest;
        OwnerUniqueId = ownerUniqueId;
        DefinitionId = definitionId;
    }
}

public sealed class QuestCompletedArgs : EventArgs
{
    public Quest Quest { get; }
    public string OwnerUniqueId { get; }
    public string DefinitionId { get; }
    public QuestCompletedArgs(Quest quest, string ownerUniqueId, string definitionId)
    {
        Quest = quest;
        OwnerUniqueId = ownerUniqueId;
        DefinitionId = definitionId;
    }
}

public sealed class QuestRemovedArgs : EventArgs
{
    public Quest Quest { get; }
    public string OwnerUniqueId { get; }
    public string DefinitionId { get; }
    /// True if the quest was completed before being removed from the journal.
    public bool WasCompleted { get; }
    public QuestRemovedArgs(Quest quest, string ownerUniqueId, string definitionId, bool wasCompleted)
    {
        Quest = quest;
        OwnerUniqueId = ownerUniqueId;
        DefinitionId = definitionId;
        WasCompleted = wasCompleted;
    }
}

public sealed class DayRefreshedArgs : EventArgs
{
    /// Number of postings the framework just placed on the daily board.
    public int DailyBoardCount { get; }
    /// Number of mail-triggered postings dispatched this day.
    public int MailCount { get; }
    public DayRefreshedArgs(int dailyBoardCount, int mailCount)
    {
        DailyBoardCount = dailyBoardCount;
        MailCount = mailCount;
    }
}
