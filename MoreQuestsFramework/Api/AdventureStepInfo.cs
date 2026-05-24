using System.Collections.Generic;

namespace MoreQuestsFramework.Api;

// Read-only snapshot of one AdventureQuest step for consumer-mod UIs (quest
// journals, debug menus). Kind is stringified so consumers don't need a
// reference to the AdventureStepKind enum across mod boundaries.
public sealed class AdventureStepInfo
{
    public string Name { get; }
    public string Kind { get; }
    public int Progress { get; }
    public int Count { get; }
    public bool Done { get; }
    public bool Active { get; }
    public string Description { get; }
    public IReadOnlyList<string> Requires { get; }
    public IReadOnlyList<string> Targets { get; }
    public IReadOnlyList<string> Items { get; }
    public string LocationName { get; }
    public string Weather { get; }
    public int MinSize { get; }
    public int MinQuality { get; }

    public AdventureStepInfo(
        string name,
        string kind,
        int progress,
        int count,
        bool done,
        bool active,
        string description,
        IReadOnlyList<string> requires,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> items,
        string locationName,
        string weather,
        int minSize,
        int minQuality)
    {
        Name = name ?? string.Empty;
        Kind = kind ?? string.Empty;
        Progress = progress;
        Count = count;
        Done = done;
        Active = active;
        Description = description ?? string.Empty;
        Requires = requires ?? System.Array.Empty<string>();
        Targets = targets ?? System.Array.Empty<string>();
        Items = items ?? System.Array.Empty<string>();
        LocationName = locationName ?? string.Empty;
        Weather = weather ?? string.Empty;
        MinSize = minSize;
        MinQuality = minQuality;
    }
}
