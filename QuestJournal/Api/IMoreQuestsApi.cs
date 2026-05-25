using System.Collections.Generic;
using StardewValley.Quests;

namespace QuestJournal.Api;

// Local duck-typed mirror of MoreQuestsFramework's IMoreQuestsApi. SMAPI's
// ModRegistry.GetApi proxies the real API against this interface so we don't
// take a ProjectReference / assembly reference on MoreQuestsFramework.dll. Only
// the slice the journal actually uses is declared. Property and method names
// must match MQF's IMoreQuestsApi exactly; the proxy is name-based.
public interface IMoreQuestsApi
{
    IReadOnlyList<IQuestRewardLine> GetRewardLines(Quest quest);

    IReadOnlyList<IAdventureStepInfo>? GetAdventureSteps(Quest quest);

    int? GetActiveStepIndex(Quest quest);
}
