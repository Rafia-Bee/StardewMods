using System.Collections.Generic;

namespace QuestJournal.Api;

// Details for one step of a multi-step quest, as exposed by the MoreQuests API.
public interface IAdventureStepInfo
{
    string Name { get; }
    string Kind { get; }
    int Progress { get; }
    int Count { get; }
    bool Done { get; }
    bool Active { get; }
    string Description { get; }
    IReadOnlyList<string> Requires { get; }
    IReadOnlyList<string> Targets { get; }
    IReadOnlyList<string> Items { get; }
    string LocationName { get; }
    string Weather { get; }
    int MinSize { get; }
    int MinQuality { get; }
}
