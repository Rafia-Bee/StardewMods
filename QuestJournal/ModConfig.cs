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

    // Journal window position, as a pixel offset from screen-centered (0,0 =
    // centered). Set by dragging the journal's title bar; persisted so the
    // window stays where you put it.
    public int JournalOffsetX { get; set; }
    public int JournalOffsetY { get; set; }

    // Pinned-HUD top-left position in UI pixels. -1 on either axis means "use
    // the default top-right anchor". Set by dragging the HUD panel.
    public int HudPinX { get; set; } = -1;
    public int HudPinY { get; set; } = -1;

    // Whole-journal zoom. 1.0 is the shipped size. Clamped at read time so a
    // bad value can't make the journal vanish or overflow the screen. A GMCM
    // slider for this lands with the step 13 config pass; for now it's a
    // config.json value.
    public float JournalScale { get; set; } = 1f;

    // Dev-only: re-watches assets/views and assets/sprites and live-reloads.
    // Off by default to avoid the perf hit and the watcher's file locks.
    public bool HotReloadViews { get; set; }
}
