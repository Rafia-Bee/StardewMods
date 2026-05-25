using System.Collections.Generic;

namespace QuestJournal.Api;

// Local duck-typed mirror of MoreQuestsFramework's AdventureStepInfo. SMAPI's
// proxy resolves property names against the real class so the journal can read
// the snapshot without a hard reference to MQF. Keep names aligned 1:1.
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
