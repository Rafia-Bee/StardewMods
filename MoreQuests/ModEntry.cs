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

    /// Per-mod scope captured at `GameLaunched`. Held so the GMCM save callback can call
    /// `FindBoard` and re-apply tile/offset edits to the live registry instance.
    internal static IMoreQuestsModApi? ModScope { get; private set; }

    /// Static accessor used by content-mod quest generators to look up their own i18n
    /// strings. The framework's `QuestContext.Helper` belongs to the framework, so its
    /// translation helper would only see the framework's i18n keys.
    internal static ITranslationHelper I18n => Instance.Helper.Translation;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded_ScanCombatFoods;
        helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    internal const string AdventureBoardAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoard";
    internal const string AdventureBoardBackgroundAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoardBackground";

    /// Mail key prefix shared by every deep-dive reward letter. The bar id + count are
    /// embedded directly in the key (e.g. `...DeepDiveReward.337.4` for 4 Iridium Bars),
    /// so the asset edit handler can build the body + attachment on the fly without any
    /// per-quest persistence.
    internal const string DeepDiveRewardKeyPrefix = "RafiaBee.MoreQuests.DeepDiveReward.";

    /// Asset edits and loads owned by the content mod:
    /// - `Data/mail` reward letters (Submarine Fuel pearl, Wizard's Ritual book, deep-dive bars).
    /// - `Mods/RafiaBee.MoreQuests/AdventureBoard` in-world wall sprite drawn at the board's
    ///   anchor tile (Mine [12,6]).
    /// - `Mods/RafiaBee.MoreQuests/AdventureBoardBackground` menu skin used for both the
    ///   cork-board view and the accept-quest popup; sheet matches vanilla `LooseSprites/Billboard`.
    private void OnAssetRequested(object? sender, StardewModdingAPI.Events.AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(AdventureBoardAssetRoot))
        {
            e.LoadFromModFile<Texture2D>("assets/AdventureBoard.png", AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(AdventureBoardBackgroundAssetRoot))
        {
            e.LoadFromModFile<Texture2D>("assets/AdventureBoardBackground.png", AssetLoadPriority.Low);
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
        ModScope = scope;

        // Custom Quest subclasses (must register before quests.json loads so generators
        // that build PreBuiltQuests of these types round-trip through SpaceCore).
        // The framework's `AdventureQuest` is already registered framework-side, so the
        // multistep Check on George quest doesn't need a content-mod-specific registration.
        scope.RegisterCustomQuestType(typeof(AnySlimeQuest));
        scope.RegisterCustomQuestType(typeof(AnyMonsterQuest));
        scope.RegisterCustomQuestType(typeof(CollectAndReportQuest));

        // Combat-food pool is auto-populated at SaveLoaded by `OnSaveLoaded_ScanCombatFoods`
        // (which reads buff data straight from `Data/Objects`, so modded foods that grant
        // Attack/Defense get picked up automatically). The legacy `RegisterCombatFood`
        // API is still live for consumer mods that want to add foods the scan misses.

        // Register every C# generator referenced by assets/quests.json.
        Generators.RegisterAll(scope);

        // Load the JSON quest pack. Each entry references a generator above. The cooldown
        // tier resolver maps the named buckets in quests.json (Short / Medium / Long) to the
        // current ModConfig values, so GMCM edits to those knobs apply on the next trigger
        // evaluation without re-loading the pack.
        scope.LoadQuestsFromMod(Helper, "assets/quests.json", ResolveCooldownTier);

        // Adventurer's Guild board (Phase 8b/8c). When the player keeps it enabled, the
        // guild board renders at the mine entrance and the mining/monster quests route
        // there. When disabled, the board doesn't render and the same quests fall back
        // onto the help-wanted board so the content stays reachable. Per-quest weights
        // still gate individual quests on top of this.
        ApplyGuildBoardRouting(scope);

        GmcmRegistration.Register(Helper, ModManifest);
    }

    /// Maps a `Trigger.CooldownTier` name from `assets/quests.json` to the current ModConfig
    /// day count. Tier names are case-insensitive. Unknown tier names log a one-time warning
    /// and fall back to the JSON's `CooldownDays` literal.
    private int? ResolveCooldownTier(string tier)
    {
        return tier?.Trim().ToLowerInvariant() switch
        {
            "short" => Config.QuestCooldownShortDays,
            "medium" => Config.QuestCooldownMediumDays,
            "long" => Config.QuestCooldownLongDays,
            _ => null
        };
    }

    /// Combat-food magnitude lookup populated by `OnSaveLoaded_ScanCombatFoods`. Maps
    /// qualified item id to `max(Attack, Defense)` rounded down from the buff's
    /// `CustomAttributes`. Used by `MonsterHunt` to filter the reward pool by the
    /// rolled target magnitude (+1 / +2 / +3). Foods registered via
    /// `IMoreQuestsApi.RegisterCombatFood` that the scan didn't see won't appear here
    /// and so won't be eligible as a magnitude-bucketed reward (they still show up in
    /// the framework's flat `GetCombatFoodPool` for any future generic consumer).
    private static readonly Dictionary<string, int> CombatFoodMagnitudes =
        new(StringComparer.OrdinalIgnoreCase);

    internal static int? GetCombatFoodMagnitude(string qualifiedItemId)
        => CombatFoodMagnitudes.TryGetValue(qualifiedItemId, out int m) ? m : null;

    /// Walks `Data/Objects` after the save loads (so every content-pack edit is live)
    /// and registers every edible food whose `Buffs` grant a non-zero Attack or
    /// Defense as a combat-buff food. Magnitude = `max(Attack, Defense)` floored to
    /// the nearest int. Re-runs on each load so configs that swap content packs
    /// between sessions pick up the right pool. Old entries are cleared first.
    private void OnSaveLoaded_ScanCombatFoods(object? sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
    {
        if (Framework == null)
            return;

        CombatFoodMagnitudes.Clear();

        var data = Helper.GameContent.Load<Dictionary<string, StardewValley.GameData.Objects.ObjectData>>("Data/Objects");
        int registered = 0;
        foreach (var (rawId, obj) in data)
        {
            if (obj == null || obj.Edibility <= 0 || obj.Buffs == null)
                continue;

            int magnitude = 0;
            foreach (var buff in obj.Buffs)
            {
                var attrs = buff?.CustomAttributes;
                if (attrs == null)
                    continue;
                int level = (int)Math.Floor(Math.Max(attrs.Attack, attrs.Defense));
                if (level > magnitude)
                    magnitude = level;
            }
            if (magnitude <= 0)
                continue;

            string qualified = "(O)" + rawId;
            CombatFoodMagnitudes[qualified] = magnitude;
            Framework.RegisterCombatFood(qualified);
            registered++;
        }

        Monitor.Log($"Scanned Data/Objects: registered {registered} combat-buff food(s).", LogLevel.Trace);
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
            ApplyAdventureBoardConfig();
        }
        else
        {
            scope.OverrideTriggerSource("Mining.SkullCavernDeepDive", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.MinesDeepDive", TriggerSource.DailyBoard);
        }
    }

    /// Mutates the registered `AdventurersGuild` board's anchor tile + draw offset to
    /// match `ModConfig`. Called once after `LoadBoardsFromMod` registers the board, and
    /// again whenever GMCM saves so live edits in the config menu take effect on the
    /// next render.
    internal static void ApplyAdventureBoardConfig()
    {
        var board = ModScope?.FindBoard("AdventurersGuild");
        if (board == null)
            return;
        board.Tile = new[] { Config.AdventureBoardTileX, Config.AdventureBoardTileY };
        board.DrawOffset = new[] { Config.AdventureBoardDrawOffsetX, Config.AdventureBoardDrawOffsetY };
    }
}
