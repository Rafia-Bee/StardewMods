using System.Collections.Generic;
using StardewValley.Quests;

namespace QuestJournal.Api;

// The MoreQuests mod's API as we see it. Lets us pull extra quest details
// (rewards, steps, objectives, giver, item requirements) for the journal.
public interface IMoreQuestsApi
{
    IReadOnlyList<IQuestRewardLine> GetRewardLines(Quest quest);

    IReadOnlyList<IAdventureStepInfo>? GetAdventureSteps(Quest quest);

    int? GetActiveStepIndex(Quest quest);

    IReadOnlyList<string>? GetObjectiveLines(Quest quest);

    string? GetDefinitionId(Quest quest);

    string? GetGiverNpc(Quest quest);

    IQuestInfo? GetQuestInfo(string definitionId);

    IQuestItemRequirement? GetItemRequirement(Quest quest);
}
