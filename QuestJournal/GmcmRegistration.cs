using StardewModdingAPI;

namespace QuestJournal;

// Wires up the Generic Mod Config Menu page for this mod.
// Builds all the config rows (keybinds, toggles, sliders, dropdowns) and the
// separate controller page. Does nothing if GMCM isn't installed.
internal static class GmcmRegistration
{
    public static void Register(IModHelper helper, IManifest manifest)
    {
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        var t = helper.Translation;

        api.Register(
            mod: manifest,
            reset: () => ModEntry.Config = new ModConfig(),
            save: () => helper.WriteConfig(ModEntry.Config));

        api.AddSectionTitle(manifest, () => t.Get("config.section.general"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.OpenJournalKey,
            v => ModEntry.Config.OpenJournalKey = v,
            () => t.Get("config.openKey"),
            () => t.Get("config.openKey.tooltip"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.TogglePinKey,
            v => ModEntry.Config.TogglePinKey = v,
            () => t.Get("config.togglePin"),
            () => t.Get("config.togglePin.tooltip"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.ToggleHudKey,
            v => ModEntry.Config.ToggleHudKey = v,
            () => t.Get("config.toggleHud"),
            () => t.Get("config.toggleHud.tooltip"));

        const string controllerPageId = "controller";
        api.AddPageLink(manifest, controllerPageId,
            () => t.Get("config.controller.link"),
            () => t.Get("config.controller.link.tooltip"));

        api.AddBoolOption(manifest,
            () => ModEntry.Config.AddGameMenuTab,
            v => ModEntry.Config.AddGameMenuTab = v,
            () => t.Get("config.gameMenuTab"),
            () => t.Get("config.gameMenuTab.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ShowCompletedTab,
            v => ModEntry.Config.ShowCompletedTab = v,
            () => t.Get("config.showCompletedTab"),
            () => t.Get("config.showCompletedTab.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ShowAllTab,
            v => ModEntry.Config.ShowAllTab = v,
            () => t.Get("config.showAllTab"),
            () => t.Get("config.showAllTab.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ShowHudPin,
            v => ModEntry.Config.ShowHudPin = v,
            () => t.Get("config.hudPin"),
            () => t.Get("config.hudPin.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.PinnedFirst,
            v => ModEntry.Config.PinnedFirst = v,
            () => t.Get("config.pinnedFirst"),
            () => t.Get("config.pinnedFirst.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.HudHoverObjectiveTooltip,
            v => ModEntry.Config.HudHoverObjectiveTooltip = v,
            () => t.Get("config.hudHoverSteps"),
            () => t.Get("config.hudHoverSteps.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DefaultPinNewQuests,
            v => ModEntry.Config.DefaultPinNewQuests = v,
            () => t.Get("config.pinNew"),
            () => t.Get("config.pinNew.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.MarkQuestsReadOnOpen,
            v => ModEntry.Config.MarkQuestsReadOnOpen = v,
            () => t.Get("config.markRead"),
            () => t.Get("config.markRead.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DebugLogging,
            v => ModEntry.Config.DebugLogging = v,
            () => t.Get("config.debugLogging"),
            () => t.Get("config.debugLogging.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.appearance"));
        api.AddNumberOption(manifest,
            () => ModEntry.Config.JournalScale,
            v => ModEntry.Config.JournalScale = v,
            () => t.Get("config.scale"),
            () => t.Get("config.scale.tooltip"),
            min: 0.7f, max: 1.5f, interval: 0.05f);
        api.AddNumberOption(manifest,
            () => ModEntry.Config.HudPinOpacity,
            v => ModEntry.Config.HudPinOpacity = v,
            () => t.Get("config.hudOpacity"),
            () => t.Get("config.hudOpacity.tooltip"),
            min: 0.2f, max: 1f, interval: 0.05f);

        api.AddTextOption(manifest,
            () => ModEntry.Config.QuestSort,
            v => ModEntry.Config.QuestSort = v,
            () => t.Get("config.sort"),
            () => t.Get("config.sort.tooltip"),
            allowedValues: new[] { "Deadline", "Alphabetical", "Giver", "Source", "Category" },
            formatAllowedValue: v => v switch
            {
                "Alphabetical" => t.Get("journal.sort.alphabetical"),
                "Giver" => t.Get("journal.sort.giver"),
                "Source" => t.Get("journal.sort.source"),
                "Category" => t.Get("journal.sort.category"),
                _ => t.Get("journal.sort.deadline")
            });

        api.AddSectionTitle(manifest, () => t.Get("config.section.cheats"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.AllowCompleteCheat,
            v => ModEntry.Config.AllowCompleteCheat = v,
            () => t.Get("config.completeCheat"),
            () => t.Get("config.completeCheat.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.AllowItemCheats,
            v => ModEntry.Config.AllowItemCheats = v,
            () => t.Get("config.itemCheats"),
            () => t.Get("config.itemCheats.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.AllowWarpCheat,
            v => ModEntry.Config.AllowWarpCheat = v,
            () => t.Get("config.warpCheat"),
            () => t.Get("config.warpCheat.tooltip"));

        api.AddPage(manifest, controllerPageId, () => t.Get("config.section.controller"));
        api.AddParagraph(manifest, () => t.Get("config.controller.note"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.PrevTabKey,
            v => ModEntry.Config.PrevTabKey = v,
            () => t.Get("config.prevTab"),
            () => t.Get("config.prevTab.tooltip"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.NextTabKey,
            v => ModEntry.Config.NextTabKey = v,
            () => t.Get("config.nextTab"),
            () => t.Get("config.nextTab.tooltip"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.EditTabKey,
            v => ModEntry.Config.EditTabKey = v,
            () => t.Get("config.editTab"),
            () => t.Get("config.editTab.tooltip"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.AddTabKey,
            v => ModEntry.Config.AddTabKey = v,
            () => t.Get("config.addTab"),
            () => t.Get("config.addTab.tooltip"));
        api.AddKeybindList(manifest,
            () => ModEntry.Config.HudActivateKey,
            v => ModEntry.Config.HudActivateKey = v,
            () => t.Get("config.hudActivate"),
            () => t.Get("config.hudActivate.tooltip"));
    }
}
