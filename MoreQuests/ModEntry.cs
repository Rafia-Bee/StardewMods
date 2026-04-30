using System;
using MoreQuests.Quests;
using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MoreQuests;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    /// Static accessor used by content-mod quest generators to look up their own i18n
    /// strings. The framework's `QuestContext.Helper` belongs to the framework, so its
    /// translation helper would only see the framework's i18n keys.
    internal static ITranslationHelper I18n => Instance.Helper.Translation;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var fw = Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (fw == null)
        {
            Monitor.Log(
                "More Quests Framework not loaded; this mod cannot register its quests. " +
                "Install RafiaBee.MoreQuestsFramework to enable More Quests content.",
                LogLevel.Error);
            return;
        }

        fw.RegistrationOpen += (_, _) => RegisterContent(fw);
    }

    private void RegisterContent(IMoreQuestsApi fw)
    {
        var scope = fw.GetModApi(ModManifest);

        // Custom Quest subclasses (must register before quests.json loads so generators
        // that build PreBuiltQuests of these types round-trip through SpaceCore).
        // The framework's `AdventureQuest` is already registered framework-side, so the
        // multistep Check on George quest doesn't need a content-mod-specific registration.
        scope.RegisterCustomQuestType(typeof(AnySlimeQuest));
        scope.RegisterCustomQuestType(typeof(CollectAndReportQuest));

        // Register every C# generator referenced by assets/quests.json.
        Generators.RegisterAll(scope);

        // Load the JSON quest pack. Each entry references a generator above.
        scope.LoadQuestsFromMod(Helper, "assets/quests.json");

        GmcmRegistration.Register(Helper, ModManifest);
    }
}
