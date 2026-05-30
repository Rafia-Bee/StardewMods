using System.Collections.ObjectModel;

namespace QuestJournal.Warp;

// Backing context for warp_dropdown.sml. Opened as a child menu when a quest
// touches more than one NPC, so the player picks who to warp to.
public sealed class WarpDropdownContext
{
    public string Title { get; }
    public ObservableCollection<WarpOptionRow> Options { get; } = new();

    public WarpDropdownContext(string title)
    {
        Title = title;
    }
}

// One pickable NPC row in the warp dropdown. Clicking it warps and (via the
// resolver closing the menu) drops the player back into the world.
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
