using System.Collections.ObjectModel;

namespace QuestJournal.Warp;

// Backs the warp dropdown menu in the UI: a title plus a list of NPC options.
// Picking an option warps the player to that NPC.
public sealed class WarpDropdownContext
{
    public string Title { get; }
    public ObservableCollection<WarpOptionRow> Options { get; } = new();

    public WarpDropdownContext(string title)
    {
        Title = title;
    }
}

public sealed class WarpOptionRow
{
    public string Label { get; }
    private readonly string _internalName;

    public WarpOptionRow(string label, string internalName)
    {
        Label = label;
        _internalName = internalName;
    }

    public void Choose() => NpcWarpResolver.Warp(_internalName);
}
