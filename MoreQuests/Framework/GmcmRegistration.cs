using StardewModdingAPI;

namespace MoreQuests.Framework;

internal static class GmcmRegistration
{
    public static void Register(IModHelper helper, IManifest manifest)
    {
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            mod: manifest,
            reset: () => ModEntry.Config = new ModConfig(),
            save: () => helper.WriteConfig(ModEntry.Config)
        );

        // Quest Board
        api.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.section.questBoard"));

        api.AddNumberOption(
            mod: manifest,
            getValue: () => ModEntry.Config.QuestsPerDay,
            setValue: v => ModEntry.Config.QuestsPerDay = v,
            name: () => helper.Translation.Get("config.questsPerDay"),
            tooltip: () => helper.Translation.Get("config.questsPerDay.tooltip"),
            min: 1,
            max: 8
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => ModEntry.Config.QuestDeadlineDays,
            setValue: v => ModEntry.Config.QuestDeadlineDays = v,
            name: () => helper.Translation.Get("config.questDeadline"),
            tooltip: () => helper.Translation.Get("config.questDeadline.tooltip"),
            min: 1,
            max: 20
        );

        // Difficulty
        api.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.section.difficulty"));

        api.AddBoolOption(
            mod: manifest,
            getValue: () => ModEntry.Config.DifficultyScaling,
            setValue: v => ModEntry.Config.DifficultyScaling = v,
            name: () => helper.Translation.Get("config.difficultyScaling"),
            tooltip: () => helper.Translation.Get("config.difficultyScaling.tooltip")
        );

        // Consequences
        api.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.section.consequences"));

        api.AddBoolOption(
            mod: manifest,
            getValue: () => ModEntry.Config.ConsequencesEnabled,
            setValue: v => ModEntry.Config.ConsequencesEnabled = v,
            name: () => helper.Translation.Get("config.consequences"),
            tooltip: () => helper.Translation.Get("config.consequences.tooltip")
        );

        // Modded content
        api.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.section.modded"));

        api.AddBoolOption(
            mod: manifest,
            getValue: () => ModEntry.Config.IncludeModdedItems,
            setValue: v => ModEntry.Config.IncludeModdedItems = v,
            name: () => helper.Translation.Get("config.moddedItems"),
            tooltip: () => helper.Translation.Get("config.moddedItems.tooltip")
        );

        api.AddBoolOption(
            mod: manifest,
            getValue: () => ModEntry.Config.IncludeModdedNPCs,
            setValue: v => ModEntry.Config.IncludeModdedNPCs = v,
            name: () => helper.Translation.Get("config.moddedNPCs"),
            tooltip: () => helper.Translation.Get("config.moddedNPCs.tooltip")
        );

        // Festival quests
        api.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.section.festivals"));

        api.AddBoolOption(
            mod: manifest,
            getValue: () => ModEntry.Config.FestivalQuestsEnabled,
            setValue: v => ModEntry.Config.FestivalQuestsEnabled = v,
            name: () => helper.Translation.Get("config.festivalQuests"),
            tooltip: () => helper.Translation.Get("config.festivalQuests.tooltip")
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => ModEntry.Config.FestivalQuestLeadDays,
            setValue: v => ModEntry.Config.FestivalQuestLeadDays = v,
            name: () => helper.Translation.Get("config.festivalLeadDays"),
            tooltip: () => helper.Translation.Get("config.festivalLeadDays.tooltip"),
            min: 1,
            max: 7
        );

        // Animal quests
        api.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.section.animals"));

        api.AddBoolOption(
            mod: manifest,
            getValue: () => ModEntry.Config.AnimalQuestsEnabled,
            setValue: v => ModEntry.Config.AnimalQuestsEnabled = v,
            name: () => helper.Translation.Get("config.animalQuests"),
            tooltip: () => helper.Translation.Get("config.animalQuests.tooltip")
        );
    }
}
