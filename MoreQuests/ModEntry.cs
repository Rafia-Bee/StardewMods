using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using MoreQuests.Quests;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Shops;

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

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
        {
            if (!IsHaySupplyRunActive())
                return;
            e.Edit(asset =>
            {
                var shops = asset.AsDictionary<string, ShopData>().Data;
                if (!shops.TryGetValue("AnimalShop", out var animalShop) || animalShop?.Items == null)
                    return;
                for (int i = animalShop.Items.Count - 1; i >= 0; i--)
                {
                    var entry = animalShop.Items[i];
                    if (entry == null) continue;
                    if (IsHayItemId(entry.ItemId))
                        animalShop.Items.RemoveAt(i);
                }
            }, AssetEditPriority.Late);
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

            // Skull Cavern Deep Dive (Row 67) pays in Radioactive Bars and uses a
            // distinct "unexpected smelting byproduct" flavour. Mines Deep Dive
            // (Row 78) keeps the standard Marlon-flavoured payout body.
            string bodyKey = barId == "910"
                ? "mail.mining.deepDiveReward.skullCavern.body"
                : "mail.mining.deepDiveReward.body";
            string body = I18n.Get(bodyKey, new { count, bar = barName }).ToString();
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
        fw.QuestRemoved += OnQuestRemoved;

        WireLivestockFollowsYou();
    }

    /// Cached LivestockFollowsYou API (duck-typed). Null until `WireLivestockFollowsYou`
    /// resolves it from the registry, and null forever on saves where LFY isn't installed.
    /// Generators read this lazily (e.g. `MarnieCowOffer` checks for the Grazing Bell id).
    internal static ILfyApi? Lfy { get; private set; }

    /// Fetches the LivestockFollowsYou API (duck-typed via the local `ILfyApi`) and
    /// pipes its follower-count read into the framework's `FollowerApiBridge`, which is
    /// what `AdventureQuest`'s Visit step's `$follower-count:N` gate consults. No-op
    /// when LFY isn't installed — Visit-step follower gates evaluate to 0 in that case
    /// and the matching quests just stay incomplete on warp.
    private void WireLivestockFollowsYou()
    {
        if (!Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.LivestockFollowsYou))
            return;
        var api = Helper.ModRegistry.GetApi<ILfyApi>(MoreQuestsFramework.ModCompat.LivestockFollowsYou);
        if (api == null)
        {
            Monitor.Log("LivestockFollowsYou is installed but its API didn't resolve; follower-count gated quests won't auto-complete.", LogLevel.Warn);
            return;
        }
        Lfy = api;
        MoreQuestsFramework.FollowerApiBridge.AnimalFollowerCount = () => api.FollowingAnimalCount;
        Monitor.Log("Wired LivestockFollowsYou follower count into the framework's FollowerApiBridge.", LogLevel.Trace);
    }

    /// Duck-typed mirror of LivestockFollowsYou's `ILivestockFollowsYouApi`. Only declares
    /// the members we actually consume so the surface area is minimal. SMAPI's
    /// `GetApi<T>` matches by member shape, so the duck-typed interface lets us avoid an
    /// assembly reference back to LFY.
    internal interface ILfyApi
    {
        int FollowingAnimalCount { get; }
        string GrazingBellQualifiedItemId { get; }
    }

    private void OnQuestRemoved(object? sender, QuestRemovedArgs e)
    {
        if (e.DefinitionId == "Animal.HaySupplyRun")
            Helper.GameContent.InvalidateCache("Data/Shops");
    }

    private void OnQuestCompleted(object? sender, QuestCompletedArgs e)
    {
        if (e.DefinitionId == "Animal.HaySupplyRun")
            Helper.GameContent.InvalidateCache("Data/Shops");
        if (e.DefinitionId == "Animal.GuntherDinosaurStudy")
            GrantUpgradedDinosaurEgg(e.Quest);
        if (e.DefinitionId == "Animal.MarnieChickenOffer")
            GrantFreeChicken();
        if (e.DefinitionId == "Animal.MarnieCowOffer")
            GrantFreeCow();
        if (e.DefinitionId != "Mining.SkullCavernDeepDive" && e.DefinitionId != "Mining.MinesDeepDive")
            return;
        Helper.GameContent.InvalidateCache("Data/mail");
    }

    /// Adopts a vanilla White Chicken directly into the first Coop on the player's farm
    /// that has a free animal slot. Bypasses `PurchaseAnimalsMenu` entirely (so Livestock
    /// Bazaar compatibility is automatic — both menus only sit on top of the same
    /// `AnimalHouse.adoptAnimal` placement path). Falls back to a gold rebate via
    /// `MarnieChickenOfferRebate` when no coop has space, so the player isn't left empty
    /// handed if their coops are full at completion time.
    private void GrantFreeChicken()
    {
        var farm = Game1.getFarm();
        if (farm == null)
        {
            FallbackChickenRebate();
            return;
        }

        foreach (var building in farm.buildings)
        {
            if (building?.GetIndoors() is not StardewValley.AnimalHouse animalHouse)
                continue;
            if (animalHouse.isFull())
                continue;
            if (!IsCoop(building))
                continue;

            var chicken = new StardewValley.FarmAnimal("White Chicken", Game1.Multiplayer.getNewID(), Game1.player.UniqueMultiplayerID);
            animalHouse.adoptAnimal(chicken);
            Monitor.Log($"Adopted a free White Chicken into '{building.buildingType.Value}' for Marnie's Chicken Offer.", LogLevel.Trace);
            return;
        }

        FallbackChickenRebate();
    }

    private void FallbackChickenRebate()
    {
        int rebate = Math.Max(0, Config.MarnieChickenOfferRebate);
        if (rebate <= 0)
            return;
        Game1.player.Money += rebate;
        Game1.addHUDMessage(new StardewValley.HUDMessage(I18n.Get("quest.animal.marnieChickenOffer.coopFullFallback").ToString(), 1));
        Monitor.Log("No empty coop slot at quest completion; paid out the Marnie chicken rebate instead.", LogLevel.Trace);
    }

    private static bool IsCoop(StardewValley.Buildings.Building building)
    {
        string type = building.buildingType.Value ?? string.Empty;
        return type.IndexOf("Coop", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// Adopts a free Dairy Cow (random White / Brown) directly into the first Barn on the
    /// player's farm with a free animal slot. Mirrors `GrantFreeChicken` for Row 44.
    /// Falls back to `MarnieCowOfferRebate` gold when every barn is full.
    private void GrantFreeCow()
    {
        var farm = Game1.getFarm();
        if (farm == null)
        {
            FallbackCowRebate();
            return;
        }

        string cowType = Game1.random.NextDouble() < 0.5 ? "White Cow" : "Brown Cow";
        foreach (var building in farm.buildings)
        {
            if (building?.GetIndoors() is not StardewValley.AnimalHouse animalHouse)
                continue;
            if (animalHouse.isFull())
                continue;
            if (!IsBarn(building))
                continue;

            var cow = new StardewValley.FarmAnimal(cowType, Game1.Multiplayer.getNewID(), Game1.player.UniqueMultiplayerID);
            animalHouse.adoptAnimal(cow);
            Monitor.Log($"Adopted a free {cowType} into '{building.buildingType.Value}' for Marnie's Cow Offer.", LogLevel.Trace);
            return;
        }

        FallbackCowRebate();
    }

    private void FallbackCowRebate()
    {
        int rebate = Math.Max(0, Config.MarnieCowOfferRebate);
        if (rebate <= 0)
            return;
        Game1.player.Money += rebate;
        Game1.addHUDMessage(new StardewValley.HUDMessage(I18n.Get("quest.animal.marnieCowOffer.barnFullFallback").ToString(), 1));
        Monitor.Log("No empty barn slot at quest completion; paid out the Marnie cow rebate instead.", LogLevel.Trace);
    }

    private static bool IsBarn(StardewValley.Buildings.Building building)
    {
        string type = building.buildingType.Value ?? string.Empty;
        return type.IndexOf("Barn", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// Returns a Dinosaur Egg whose Quality is one tier above whatever the player
    /// delivered. Vanilla quality ladder: 0 regular → 1 silver → 2 gold → 4 iridium
    /// (3 is skipped per vanilla). Iridium stays iridium. Reads the captured quality
    /// from the framework's `MoreQuestsItemDeliveryQuest.deliveredQuality` field.
    private void GrantUpgradedDinosaurEgg(StardewValley.Quests.Quest quest)
    {
        int delivered = quest is MoreQuestsItemDeliveryQuest idq ? idq.deliveredQuality.Value : 0;
        int upgraded = delivered switch
        {
            <= 0 => 1, // regular → silver
            1 => 2,    // silver → gold
            2 => 4,    // gold → iridium
            _ => 4     // iridium / unknown → iridium
        };

        var egg = new StardewValley.Object("107", 1) { Quality = upgraded };
        if (!Game1.player.addItemToInventoryBool(egg))
            Game1.createItemDebris(egg, Game1.player.getStandingPosition(), 2);
    }

    private void OnQuestAccepted(object? sender, QuestAcceptedArgs e)
    {
        if (e.DefinitionId == "Animal.HaySupplyRun")
        {
            Helper.GameContent.InvalidateCache("Data/Shops");
            return;
        }
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

    /// True when the player has an active Marnie hay-delivery quest in their journal.
    /// Matched by title rather than item id since other Animal quests (Marnie's Cow Offer)
    /// also ask for Hay (O)178; the hay-supply title is distinct.
    private bool IsHaySupplyRunActive()
    {
        var log = Game1.player?.questLog;
        if (log == null || log.Count == 0)
            return false;
        string hayTitle = I18n.Get("quest.animal.hay.title").ToString();
        if (string.IsNullOrEmpty(hayTitle))
            return false;
        foreach (var q in log)
        {
            if (q == null || q.completed.Value)
                continue;
            if (string.Equals(q.questTitle, hayTitle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool IsHayItemId(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;
        return itemId == "178" || itemId == "(O)178";
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
