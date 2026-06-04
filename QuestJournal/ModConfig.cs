using StardewModdingAPI.Utilities;

namespace QuestJournal;

public sealed class ModConfig
{
    public KeybindList OpenJournalKey { get; set; } = KeybindList.Parse("F6");

    // Controller tab switching. The tab rail floats above the frame, so a
    // gamepad can't focus it; these cycle the tabs instead. Default to both the
    // shoulder and trigger on each side, matching common Stardew menus.
    public KeybindList PrevTabKey { get; set; } = KeybindList.Parse("LeftShoulder, LeftTrigger");
    public KeybindList NextTabKey { get; set; } = KeybindList.Parse("RightShoulder, RightTrigger");

    // Opens the selected custom tab's editor. The "+"/"Edit tabs" controls float
    // too, so a gamepad can't reach them; this opens the editor straight from
    // the selected custom tab.
    public KeybindList EditTabKey { get; set; } = KeybindList.Parse("ControllerY");

    // Opens the new-tab editor. The floating "+" control can't take gamepad
    // focus, so this gives controllers a direct way to create a custom tab.
    public KeybindList AddTabKey { get; set; } = KeybindList.Parse("ControllerX");

    // Opens the journal to the pinned quest under the controller pointer (moved
    // with the right stick). The HUD is a world overlay, not a menu, so a pad
    // can't otherwise activate it. Only fires when the pointer is over an entry,
    // so A still works for world actions everywhere else.
    public KeybindList HudActivateKey { get; set; } = KeybindList.Parse("ControllerA");

    public bool AddGameMenuTab { get; set; } = true;
    public bool ShowHudPin { get; set; } = true;

    // When on, hovering a pinned quest on the HUD pops a tooltip listing all of
    // its remaining steps. Handy for multi-step quests, where the HUD only has
    // room for one step. Only shows when there is more than the one line already
    // on the HUD.
    public bool HudHoverObjectiveTooltip { get; set; } = true;
    public bool AllowItemCheats { get; set; }
    public bool AllowWarpCheat { get; set; }
    public bool AllowCompleteCheat { get; set; }
    public bool EnableLookupAnythingIntegration { get; set; } = true;
    public bool DefaultPinNewQuests { get; set; }

    // When on, opening this journal counts as checking your quests, so the
    // vanilla quest button stops flashing its "new quest" mark. On by default
    // since most people use this journal instead of the old one.
    public bool MarkQuestsReadOnOpen { get; set; } = true;

    // How the quests list is ordered. Stored by name (Deadline, Alphabetical,
    // Giver, Source, Category). Set from the dropdown in the journal or from
    // GMCM; a bad value falls back to Deadline at read time.
    public string QuestSort { get; set; } = "Deadline";

    // When on, the journal writes its chatty status logs (everything below a
    // warning) to the SMAPI console. Warnings and errors always show. On by
    // default for now while the mod is new.
    public bool DebugLogging { get; set; } = true;

    // Journal window position, as a pixel offset from screen-centered (0,0 =
    // centered). Set by dragging the journal's title bar; persisted so the
    // window stays where you put it.
    public int JournalOffsetX { get; set; }
    public int JournalOffsetY { get; set; }

    // Pinned-HUD top-left position in UI pixels. -1 on either axis means "use
    // the default top-right anchor". Set by dragging the HUD panel.
    public int HudPinX { get; set; } = -1;
    public int HudPinY { get; set; } = -1;

    // How see-through the pinned-HUD panel is. 1.0 is solid, lower fades it
    // toward invisible. Clamped at read time so a bad value can't make it vanish.
    public float HudPinOpacity { get; set; } = 1f;

    // Whole-journal zoom. 1.0 is the shipped size. Clamped at read time so a
    // bad value can't make the journal vanish or overflow the screen. A GMCM
    // slider for this lands with the step 13 config pass; for now it's a
    // config.json value.
    public float JournalScale { get; set; } = 1f;

    // Dev-only: re-watches assets/views and assets/sprites and live-reloads.
    // Off by default to avoid the perf hit and the watcher's file locks.
    public bool HotReloadViews { get; set; }
}
