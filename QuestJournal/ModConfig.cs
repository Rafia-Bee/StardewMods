using StardewModdingAPI.Utilities;

namespace QuestJournal;

public sealed class ModConfig
{
    public KeybindList OpenJournalKey { get; set; } = KeybindList.Parse("F6");
    public bool AddGameMenuTab { get; set; } = true;
    public bool ShowHudPin { get; set; } = true;
    public bool AllowItemCheats { get; set; }
    public bool AllowWarpCheat { get; set; }
    public bool EnableLookupAnythingIntegration { get; set; } = true;
    public bool DefaultPinNewQuests { get; set; }

    // Dev-only: re-watches assets/views and assets/sprites and live-reloads.
    // Off by default to avoid the perf hit and the watcher's file locks.
    public bool HotReloadViews { get; set; }
}
