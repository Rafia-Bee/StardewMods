using StardewModdingAPI.Utilities;

namespace QuestJournal;

// All the user settings for the mod (keybinds, toggles, offsets, scale).
// This is what gets saved to config.json and shown in the GMCM options menu.
public sealed class ModConfig
{
    public KeybindList OpenJournalKey { get; set; } = KeybindList.Parse("F6");

    public KeybindList PrevTabKey { get; set; } = KeybindList.Parse("LeftShoulder, LeftTrigger");
    public KeybindList NextTabKey { get; set; } = KeybindList.Parse("RightShoulder, RightTrigger");

    public KeybindList EditTabKey { get; set; } = KeybindList.Parse("ControllerY");

    public KeybindList AddTabKey { get; set; } = KeybindList.Parse("ControllerX");

    public KeybindList HudActivateKey { get; set; } = KeybindList.Parse("ControllerA");

    public KeybindList TogglePinKey { get; set; } = KeybindList.Parse("P");
    public KeybindList ToggleHudKey { get; set; } = new KeybindList();

    public bool AddGameMenuTab { get; set; } = true;
    public bool ReplaceVanillaQuestLog { get; set; }
    public bool ShowHudPin { get; set; } = true;
    public bool PinnedFirst { get; set; } = true;
    public bool ShowCompletedTab { get; set; } = true;
    public bool ShowAllTab { get; set; } = true;
    public bool HideCompletedInOtherTabs { get; set; }

    public bool HudHoverObjectiveTooltip { get; set; } = true;
    public bool AllowItemCheats { get; set; }
    public bool AllowWarpCheat { get; set; }
    public bool AllowCompleteCheat { get; set; }
    public bool EnableLookupAnythingIntegration { get; set; } = true;
    public bool DefaultPinNewQuests { get; set; }

    public bool MarkQuestsReadOnOpen { get; set; } = true;

    public string QuestSort { get; set; } = "Deadline";

    public bool DebugLogging { get; set; } = true;

    public int JournalOffsetX { get; set; }
    public int JournalOffsetY { get; set; }

    public int HudPinX { get; set; } = -1;
    public int HudPinY { get; set; } = -1;

    public float HudPinOpacity { get; set; } = 1f;

    public float JournalScale { get; set; } = 1f;

    public bool HotReloadViews { get; set; }
}
