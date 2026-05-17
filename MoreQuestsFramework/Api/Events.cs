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

public enum QuestRemovalReason
{
    Completed,
    Expired,
    Cancelled
}

public sealed class QuestRemovedArgs : EventArgs
{
    public Quest Quest { get; }
    public string OwnerUniqueId { get; }
    public string DefinitionId { get; }
    public QuestRemovalReason Reason { get; }
    public bool WasCompleted => Reason == QuestRemovalReason.Completed;
    public QuestRemovedArgs(Quest quest, string ownerUniqueId, string definitionId, QuestRemovalReason reason)
    {
        Quest = quest;
        OwnerUniqueId = ownerUniqueId;
        DefinitionId = definitionId;
        Reason = reason;
    }
}

public sealed class DayRefreshedArgs : EventArgs
{
    public int DailyBoardCount { get; }
    public int MailCount { get; }
    public DayRefreshedArgs(int dailyBoardCount, int mailCount)
    {
        DailyBoardCount = dailyBoardCount;
        MailCount = mailCount;
    }
}
