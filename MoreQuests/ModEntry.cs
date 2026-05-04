using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using MoreQuests.Quests;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuests;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    /// Cached reference to the framework API. Set when `OnGameLaunched` first resolves
    /// the API; quest generators read framework registries (combat-food pool, etc.)
    /// through this handle since the framework's own `ModEntry.Instance` is `internal`.
    internal static IMoreQuestsApi? Framework { get; private set; }

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

    /// Mail key prefix shared by every deep-dive reward letter. The bar id + count are
    /// embedded directly in the key (e.g. `...DeepDiveReward.337.4` for 4 Iridium Bars),
    /// so the asset edit handler can build the body + attachment on the fly without any
    /// per-quest persistence.
    internal const string DeepDiveRewardKeyPrefix = "RafiaBee.MoreQuests.DeepDiveReward.";

    /// Asset edits and loads owned by the content mod:
    /// - `Data/mail` reward letters (Submarine Fuel pearl, Wizard's Ritual book, deep-dive bars).
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

            PopulateDeepDiveRewardLetters(mail);
        });
    }

    /// Scans the player's mail collections for deep-dive reward keys (queued by the
    /// quest's `MailReward` on completion) and authors a body + bar attachment for each.
    /// The key suffix carries the parameters: `{barId}.{count}`. Skipped entirely when
    /// no save is loaded so SaveLoaded passes don't crash on a null player.
    private void PopulateDeepDiveRewardLetters(IDictionary<string, string> mail)
    {
        if (Game1.player == null)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectKeys(Game1.player.mailForTomorrow, seen);
        CollectKeys(Game1.player.mailbox, seen);
        CollectKeys(Game1.player.mailReceived, seen);

        foreach (var key in seen)
        {
            if (!key.StartsWith(DeepDiveRewardKeyPrefix, StringComparison.Ordinal))
                continue;
            string suffix = key.Substring(DeepDiveRewardKeyPrefix.Length);
            int sep = suffix.IndexOf('.');
            if (sep <= 0 || sep == suffix.Length - 1)
                continue;
            string barId = suffix.Substring(0, sep);
            if (!int.TryParse(suffix.Substring(sep + 1), out int count) || count <= 0)
                continue;

            string barName;
            try
            {
                barName = ItemRegistry.GetData("(O)" + barId)?.DisplayName ?? barId;
            }
            catch
            {
                barName = barId;
            }

            string body = I18n.Get("mail.mining.deepDiveReward.body", new { count, bar = barName }).ToString();
            mail[key] = body + $"%item object {barId} {count} %%";
        }
    }

    private static void CollectKeys(System.Collections.IEnumerable source, HashSet<string> sink)
    {
        if (source == null) return;
        foreach (var entry in source)
            if (entry is string s && !string.IsNullOrEmpty(s))
                sink.Add(s);
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

        Framework = fw;
        fw.RegistrationOpen += (_, _) => RegisterContent(fw);
        // Deep-dive reward letters are parameterised — when one completes, invalidate
        // `Data/mail` so the asset edit re-runs and populates the new key's body before
        // vanilla reads it on the next mailbox open.
        fw.QuestCompleted += OnQuestCompleted;
        // Phase 9.5d: grant the Tub o' Flowers crafting recipe up-front when the RSV
        // Gathering quest is accepted, so the player can craft tubs to ship without
        // hunting down the recipe mid-quest. CSV row 25's "give recipe with quest letter
        // if not known" requirement.
        fw.QuestAccepted += OnQuestAccepted;
    }

    private void OnQuestCompleted(object? sender, QuestCompletedArgs e)
    {
        if (e.DefinitionId != "Mining.SkullCavernDeepDive" && e.DefinitionId != "Mining.MinesDeepDive")
            return;
        Helper.GameContent.InvalidateCache("Data/mail");
    }

    private void OnQuestAccepted(object? sender, QuestAcceptedArgs e)
    {
        if (e.DefinitionId != "Festival.RidgesideGatheringDecor")
            return;
        string recipe = string.IsNullOrWhiteSpace(Config.RsvTubOFlowersRecipeName)
            ? "Tub o' Flowers"
            : Config.RsvTubOFlowersRecipeName;
        var crafting = Game1.player?.craftingRecipes;
        if (crafting == null || crafting.ContainsKey(recipe))
            return;
        crafting.Add(recipe, 0);
        Monitor.Log($"Granted '{recipe}' crafting recipe for Ridgeside Gathering quest.", LogLevel.Trace);
    }

    private void RegisterContent(IMoreQuestsApi fw)
    {
        var scope = fw.GetModApi(ModManifest);

        // Custom Quest subclasses (must register before quests.json loads so generators
        // that build PreBuiltQuests of these types round-trip through SpaceCore).
        // The framework's `AdventureQuest` is already registered framework-side, so the
        // multistep Check on George quest doesn't need a content-mod-specific registration.
        scope.RegisterCustomQuestType(typeof(AnySlimeQuest));
        scope.RegisterCustomQuestType(typeof(AnyMonsterQuest));
        scope.RegisterCustomQuestType(typeof(CollectAndReportQuest));

        // Seed the framework's combat-food pool with the vanilla combat-buff foods used
        // by Monster Hunt. Other mods can extend the pool through `IMoreQuestsApi.RegisterCombatFood`.
        SeedCombatFoodPool(fw);

        // Register every C# generator referenced by assets/quests.json.
        Generators.RegisterAll(scope);

        // Load the JSON quest pack. Each entry references a generator above.
        scope.LoadQuestsFromMod(Helper, "assets/quests.json");

        // Adventurer's Guild board (Phase 8b/8c). When the player keeps it enabled, the
        // guild board renders at the mine entrance and the mining/monster quests route
        // there. When disabled, the board doesn't render and the same quests fall back
        // onto the help-wanted board so the content stays reachable. Per-quest weights
        // still gate individual quests on top of this.
        ApplyGuildBoardRouting(scope);

        GmcmRegistration.Register(Helper, ModManifest);
    }

    /// Vanilla combat-buff foods used as the default Monster Hunt reward pool. Mirrors
    /// the curated list in plan.md §9.5a row 52: Crab Cakes / Pepper Poppers / Eggplant
    /// Parmesan / Spicy Eel / Tom Kha Soup. Other mods can extend the pool through
    /// `IMoreQuestsApi.RegisterCombatFood`.
    private static readonly string[] VanillaCombatFoods =
    {
        "(O)220", // Crab Cakes
        "(O)215", // Pepper Poppers
        "(O)218", // Eggplant Parmesan
        "(O)226", // Spicy Eel
        "(O)730"  // Tom Kha Soup
    };

    private void SeedCombatFoodPool(IMoreQuestsApi fw)
    {
        foreach (string id in VanillaCombatFoods)
            fw.RegisterCombatFood(id);
    }

    /// Wires up the routing for the Adventurer's Guild board based on
    /// `EnableAdventurersGuildBoard`. When on, mining/monster quests authored as
    /// `DailyBoard` (Mining.BasicSlimeClearing + the framework's Vanilla.SlayMonster
    /// wrapper) flip to `CustomBoard` so they post on the guild. When off, the guild
    /// board never registers and the quests authored as `CustomBoard` (the deep dives)
    /// flip back to `DailyBoard` so they reach the player via the help-wanted board.
    private void ApplyGuildBoardRouting(IMoreQuestsModApi scope)
    {
        if (Config.EnableAdventurersGuildBoard)
        {
            scope.LoadBoardsFromMod(Helper, "assets/boards.json");
            scope.OverrideTriggerSource("Mining.BasicSlimeClearing", TriggerSource.CustomBoard);
            scope.OverrideTriggerSource("Vanilla.SlayMonster", TriggerSource.CustomBoard);
        }
        else
        {
            scope.OverrideTriggerSource("Mining.SkullCavernDeepDive", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.MinesDeepDive", TriggerSource.DailyBoard);
        }
    }
}
