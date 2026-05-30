using StardewModdingAPI;

namespace QuestJournal;

// Registers the Quest Journal settings with GMCM. GMCM is optional, so this
// no-ops when it isn't installed (config.json still works either way).
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
        api.AddBoolOption(manifest,
            () => ModEntry.Config.AddGameMenuTab,
            v => ModEntry.Config.AddGameMenuTab = v,
            () => t.Get("config.gameMenuTab"),
            () => t.Get("config.gameMenuTab.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ShowHudPin,
            v => ModEntry.Config.ShowHudPin = v,
            () => t.Get("config.hudPin"),
            () => t.Get("config.hudPin.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DefaultPinNewQuests,
            v => ModEntry.Config.DefaultPinNewQuests = v,
            () => t.Get("config.pinNew"),
            () => t.Get("config.pinNew.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.appearance"));
        api.AddNumberOption(manifest,
            () => ModEntry.Config.JournalScale,
            v => ModEntry.Config.JournalScale = v,
            () => t.Get("config.scale"),
            () => t.Get("config.scale.tooltip"),
            min: 0.7f, max: 1.5f, interval: 0.05f);

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
    }
}
