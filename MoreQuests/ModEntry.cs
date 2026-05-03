using System;
using Microsoft.Xna.Framework.Graphics;
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
        helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    internal const string AdventureBoardAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoard";

    /// Asset edits and loads owned by the content mod:
    /// - `Data/mail` reward letters (Submarine Fuel pearl, Wizard's Ritual book).
    /// - `Mods/RafiaBee.MoreQuests/AdventureBoard` placeholder texture for the
    ///   Adventurer's Guild custom board declared in `assets/boards.json`.
    private void OnAssetRequested(object? sender, StardewModdingAPI.Events.AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(AdventureBoardAssetRoot))
        {
            e.LoadFromModFile<Texture2D>("assets/AdventureBoard.png", AssetLoadPriority.Low);
            return;
        }

        if (!e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
            return;
        e.Edit(asset =>
        {
            var mail = asset.AsDictionary<string, string>().Data;
            // `%item object 797 1 %%` attaches a Pearl to the letter. Vanilla mail format:
            // the trailing `%%` closes the token block.
            string submarineBody = I18n.Get("mail.festival.submarineFuelReward.body").ToString();
            mail["RafiaBee.MoreQuests.SubmarineFuelReward"] = submarineBody + "%item object 797 1 %%";

            // Book_Mystery is a 1.6 string-id item; the `%item object` token accepts string
            // ids the same way it accepts numeric ones.
            string wizardBody = I18n.Get("mail.festival.wizardsRitualReward.body").ToString();
            mail["RafiaBee.MoreQuests.WizardsRitualReward"] = wizardBody + "%item object Book_Mystery 1 %%";
        });
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

        // Custom boards (Phase 8b). Adventurer's Guild board lands here; quest content
        // that targets it ships in 8c.
        scope.LoadBoardsFromMod(Helper, "assets/boards.json");

        GmcmRegistration.Register(Helper, ModManifest);
    }
}
