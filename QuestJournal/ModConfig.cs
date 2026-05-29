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

    // Whole-journal zoom. 1.0 is the shipped size. Clamped at read time so a
    // bad value can't make the journal vanish or overflow the screen. A GMCM
    // slider for this lands with the step 13 config pass; for now it's a
    // config.json value.
    public float JournalScale { get; set; } = 1f;

    // Dev-only: re-watches assets/views and assets/sprites and live-reloads.
    // Off by default to avoid the perf hit and the watcher's file locks.
    public bool HotReloadViews { get; set; }
}
