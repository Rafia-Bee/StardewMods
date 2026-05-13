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

    /// Cached framework API. Generators use this since the framework's ModEntry.Instance is internal.
    internal static IMoreQuestsApi? Framework { get; private set; }

    /// Per-mod scope held so the GMCM save callback can re-apply board tile/offset edits.
    internal static IMoreQuestsModApi? ModScope { get; private set; }

    /// i18n accessor for content-mod generators. Framework's QuestContext.Helper only sees framework keys.
    internal static ITranslationHelper I18n => Instance.Helper.Translation;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.World.BuildingListChanged += OnBuildingListChanged;
    }

    internal const string AdventureBoardAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoard";
    internal const string AdventureBoardBackgroundAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoardBackground";

    internal const string EggBasketCreamId = "RafiaBee.MoreQuests.EggBasketCream";
    internal const string EggBasketPinkId = "RafiaBee.MoreQuests.EggBasketPink";
    internal const string EggBasketRusticId = "RafiaBee.MoreQuests.EggBasketRustic";
    internal const string EggBasketCreamTexture = "Mods/RafiaBee.MoreQuests/EggBasketCream";
    internal const string EggBasketPinkTexture = "Mods/RafiaBee.MoreQuests/EggBasketPink";
    internal const string EggBasketRusticTexture = "Mods/RafiaBee.MoreQuests/EggBasketRustic";

    /// Mail key prefix for deep-dive reward letters. Bar id + count are embedded in the key
    /// (e.g. `...DeepDiveReward.337.4` for 4 Iridium Bars) so the asset edit can build the
    /// body and attachment without needing per-quest persistence.
    internal const string DeepDiveRewardKeyPrefix = "RafiaBee.MoreQuests.DeepDiveReward.";

    /// Mail key prefix for Leah's painting reward. Suffix is `<animal>.<frame>` (e.g.
    /// `...LeahPaintingReward.BabyBlueChicken.wood`). The asset edit reads the suffix and
    /// builds a body + furniture attachment matching the painting the player earned.
    internal const string LeahPaintingRewardKeyPrefix = "RafiaBee.MoreQuests.LeahPaintingReward.";

    /// Unqualified furniture id prefix for Leah's paintings. Full id is
    /// `<prefix><animal>.<frame>`; qualified is `(F)<prefix><animal>.<frame>`.
    internal const string LeahPaintingFurnitureIdPrefix = "RafiaBee.MoreQuests.LeahPainting.";

    /// Asset name root for painting textures. Full path is `<root>/<animal>_<frame>`.
    internal const string LeahPaintingTextureRoot = "Mods/RafiaBee.MoreQuests/LeahPainting";

    /// File-system folder (relative to the mod) holding the 51 painting PNGs.
    internal const string LeahPaintingAssetFolder = "assets/leahPaintings";

    /// Animal sprite names. Matches the filename stem in assets/leahPaintings.
    internal static readonly string[] LeahPaintingAnimals =
    {
        "BabyBlueChicken", "BabyBrownChicken", "BabyBrownCow", "BabyGoat",
        "BabyGoldenChicken", "BabyOstrich", "BabyPig", "BabyRabbit", "BabySheep",
        "BabyVoidChicken", "BabyWhiteChicken", "BabyWhiteCow",
        "BlueChicken", "BrownChicken", "BrownCow", "Dinosaur", "Duck"
    };

    /// Lowercase frame keys. Matches the filename suffix and the GMCM-stored config value
    /// (stored title-cased; we lowercase before encoding into the key/path).
    internal static readonly string[] LeahPaintingFrames = { "burgandy", "night", "wood" };

    /// Title-cased frame options shown in GMCM. Stored values are case-insensitive.
    internal static readonly string[] LeahPaintingFrameOptions = { "Wood", "Burgandy", "Night" };

    internal static string NormalizeLeahPaintingFrame(string? configValue)
    {
        if (string.IsNullOrWhiteSpace(configValue))
            return "wood";
        string lower = configValue.Trim().ToLowerInvariant();
        foreach (var f in LeahPaintingFrames)
            if (lower == f) return f;
        return "wood";
    }

    /// Asset edits and loads owned by the content mod: Data/mail reward letters, the
    /// AdventureBoard wall sprite, and its menu background.
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

        if (e.NameWithoutLocale.IsEquivalentTo(EggBasketCreamTexture))
        {
            e.LoadFromModFile<Texture2D>("assets/EggBasketCream.png", AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(EggBasketPinkTexture))
        {
            e.LoadFromModFile<Texture2D>("assets/EggBasketPink.png", AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(EggBasketRusticTexture))
        {
            e.LoadFromModFile<Texture2D>("assets/EggBasketRustic.png", AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.StartsWith(LeahPaintingTextureRoot + "/"))
        {
            string stem = e.NameWithoutLocale.BaseName.Substring(LeahPaintingTextureRoot.Length + 1);
            if (IsValidLeahPaintingStem(stem))
            {
                e.LoadFromModFile<Texture2D>($"{LeahPaintingAssetFolder}/{stem}.png", AssetLoadPriority.Low);
                return;
            }
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                foreach (var animal in LeahPaintingAnimals)
                {
                    foreach (var frame in LeahPaintingFrames)
                    {
                        string id = $"{LeahPaintingFurnitureIdPrefix}{animal}.{frame}";
                        string displayName = I18n.Get($"item.leahPainting.{animal}.name", new { frame = I18n.Get($"item.leahPainting.frame.{frame}").ToString() }).ToString();
                        string texture = $"{LeahPaintingTextureRoot}/{animal}_{frame}";
                        // Format: name/type/tilesheetSize/boundingBox/rotations/price/placement/displayName/spriteIndex/texture
                        // tilesheetSize -1 = default 2x2 for paintings, boundingBox -1 = default 2x2, indoors-only via placement 0.
                        data[id] = $"{displayName}/painting/-1/-1/1/0/0/{displayName}/0/{texture}";
                    }
                }
            }, AssetEditPriority.Default);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, StardewValley.GameData.Objects.ObjectData>().Data;
                data[EggBasketCreamId] = BuildEggBasketObject(
                    I18n.Get("item.eggBasket.cream.name").ToString(),
                    I18n.Get("item.eggBasket.cream.description").ToString(),
                    EggBasketCreamTexture);
                data[EggBasketPinkId] = BuildEggBasketObject(
                    I18n.Get("item.eggBasket.pink.name").ToString(),
                    I18n.Get("item.eggBasket.pink.description").ToString(),
                    EggBasketPinkTexture);
                data[EggBasketRusticId] = BuildEggBasketObject(
                    I18n.Get("item.eggBasket.rustic.name").ToString(),
                    I18n.Get("item.eggBasket.rustic.description").ToString(),
                    EggBasketRusticTexture);
            }, AssetEditPriority.Default);
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

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
        {
            if (Game1.player == null)
                return;
            if (!Game1.player.modData.TryGetValue(FreeSiloCreditModDataKey, out string? flag) || flag != "true")
                return;
            e.Edit(asset =>
            {
                var buildings = asset.AsDictionary<string, StardewValley.GameData.Buildings.BuildingData>().Data;
                if (!buildings.TryGetValue("Silo", out var silo) || silo == null)
                    return;
                silo.BuildCost = 0;
                if (silo.BuildMaterials != null)
                    silo.BuildMaterials.Clear();
            }, AssetEditPriority.Late);
            return;
        }

        if (!e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
            return;
        e.Edit(asset =>
        {
            var mail = asset.AsDictionary<string, string>().Data;
            // `%item object 797 1 %%` attaches a Pearl. Trailing `%%` closes the token block.
            string submarineBody = I18n.Get("mail.festival.submarineFuelReward.body").ToString();
            mail["RafiaBee.MoreQuests.SubmarineFuelReward"] = submarineBody + "%item object 797 1 %%";

            // Book_Mystery is a 1.6 string-id item. The `%item object` token accepts string ids fine.
            string wizardBody = I18n.Get("mail.festival.wizardsRitualReward.body").ToString();
            mail["RafiaBee.MoreQuests.WizardsRitualReward"] = wizardBody + "%item object Book_Mystery 1 %%";

            PopulateDeepDiveRewardLetters(mail);
            PopulateLeahPaintingRewardLetters(mail);
        });
    }

    private static bool IsValidLeahPaintingStem(string stem)
    {
        int sep = stem.LastIndexOf('_');
        if (sep <= 0 || sep == stem.Length - 1)
            return false;
        string animal = stem.Substring(0, sep);
        string frame = stem.Substring(sep + 1);
        bool animalOk = false;
        foreach (var a in LeahPaintingAnimals) if (a == animal) { animalOk = true; break; }
        if (!animalOk) return false;
        foreach (var f in LeahPaintingFrames) if (f == frame) return true;
        return false;
    }

    /// Walks the player's mail for Leah painting reward keys and writes one body per key.
    /// Key suffix is `<animal>.<frame>`; the body uses the animal's display name and attaches
    /// the matching `(F)<furnitureId>`.
    private void PopulateLeahPaintingRewardLetters(IDictionary<string, string> mail)
    {
        if (Game1.player == null)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectKeys(Game1.player.mailForTomorrow, seen);
        CollectKeys(Game1.player.mailbox, seen);
        CollectKeys(Game1.player.mailReceived, seen);

        foreach (var key in seen)
        {
            if (!key.StartsWith(LeahPaintingRewardKeyPrefix, StringComparison.Ordinal))
                continue;
            string suffix = key.Substring(LeahPaintingRewardKeyPrefix.Length);
            int sep = suffix.LastIndexOf('.');
            if (sep <= 0 || sep == suffix.Length - 1)
                continue;
            string animal = suffix.Substring(0, sep);
            string frame = suffix.Substring(sep + 1);

            string animalDisplay = I18n.Get($"item.leahPainting.animal.{animal}").ToString();
            string body = I18n.Get("mail.animal.leahFarmPainting.body", new { animal = animalDisplay }).ToString();
            string furnitureId = $"{LeahPaintingFurnitureIdPrefix}{animal}.{frame}";
            mail[key] = body + $"%item id (F){furnitureId} %%";
        }
    }

    /// Scans the player's mail for deep-dive reward keys and authors a body + bar attachment for each.
    /// Key suffix is `{barId}.{count}`. Skipped when no save is loaded.
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

            // Skull Cavern pays in Radioactive Bars with the "unexpected smelting byproduct"
            // flavour. Mines Deep Dive keeps the standard Marlon payout body.
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
        // Deep-dive reward letters are parameterised. When one completes, invalidate
        // Data/mail so the asset edit re-runs and writes the new key's body before
        // vanilla reads it on the next mailbox open.
        fw.QuestCompleted += OnQuestCompleted;
        // Grant the Tub o' Flowers recipe up-front on RSV Gathering accept so the player
        // doesn't have to hunt down the recipe mid-quest.
        fw.QuestAccepted += OnQuestAccepted;
        fw.QuestRemoved += OnQuestRemoved;

        WireLivestockFollowsYou();
    }

    /// Cached LivestockFollowsYou API (duck-typed). Null when LFY isn't installed.
    /// Generators read this lazily (e.g. MarnieCowOffer checks for the Grazing Bell id).
    internal static ILfyApi? Lfy { get; private set; }

    /// Pipes LFY's follower count into the framework's FollowerApiBridge so AdventureQuest's
    /// `$follower-count:N` gate works. No-op when LFY isn't installed (gates evaluate to 0).
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
        if (e.DefinitionId == "Animal.RobinSiloOfferCoop" || e.DefinitionId == "Animal.RobinSiloOfferBarn")
            GrantFreeSilo();
        if (e.DefinitionId == "Animal.LeahFarmPainting")
        {
            Helper.GameContent.InvalidateCache("Data/mail");
            return;
        }
        if (e.DefinitionId != "Mining.SkullCavernDeepDive" && e.DefinitionId != "Mining.MinesDeepDive")
            return;
        Helper.GameContent.InvalidateCache("Data/mail");
    }

    /// Adopts a White Chicken into the first Coop with a free slot. Skips PurchaseAnimalsMenu,
    /// so Livestock Bazaar compatibility comes for free (both menus go through AnimalHouse.adoptAnimal).
    /// Falls back to a gold rebate when every coop is full.
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

    /// Adopts a random White/Brown cow into the first Barn with a free slot.
    /// Mirrors GrantFreeChicken. Falls back to a gold rebate when every barn is full.
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

    /// ModData flag for Robin's free-silo voucher. Set on Silo Offer completion, cleared
    /// when the player builds any Silo. While set, OnAssetRequested zeroes the Silo's
    /// BuildCost and BuildMaterials so it shows free in Robin's menu.
    internal const string FreeSiloCreditModDataKey = "RafiaBee.MoreQuests.FreeSiloCredit";

    /// Sets the voucher flag and invalidates Data/Buildings so Robin's menu picks up the
    /// zeroed Silo on next open. Player still picks when/where to place it. Skipped if a
    /// Silo already exists (the generator's safety check should have nulled the posting,
    /// but guards against a silo being built mid-quest).
    private void GrantFreeSilo()
    {
        var farm = Game1.getFarm();
        if (farm != null)
        {
            foreach (var b in farm.buildings)
            {
                if (string.Equals(b.buildingType.Value, "Silo", StringComparison.OrdinalIgnoreCase))
                {
                    Monitor.Log("Player already has a Silo by quest completion; skipping free-silo voucher.", LogLevel.Trace);
                    return;
                }
            }
        }

        Game1.player.modData[FreeSiloCreditModDataKey] = "true";
        Helper.GameContent.InvalidateCache("Data/Buildings");
        Game1.addHUDMessage(new StardewValley.HUDMessage(I18n.Get("quest.animal.robinSilo.voucher").ToString(), 1));
        Monitor.Log("Set the Robin free-silo voucher; Silo in Robin's carpenter menu will cost 0g and no materials until built.", LogLevel.Trace);
    }

    /// Clears the voucher flag when a Silo is built. One-shot per quest completion;
    /// demolishing a silo never re-grants the voucher.
    private void OnBuildingListChanged(object? sender, StardewModdingAPI.Events.BuildingListChangedEventArgs e)
    {
        if (Game1.player == null)
            return;
        if (!Game1.player.modData.TryGetValue(FreeSiloCreditModDataKey, out string? flag) || flag != "true")
            return;
        if (e.Location is not StardewValley.Farm)
            return;
        foreach (var b in e.Added)
        {
            if (string.Equals(b.buildingType.Value, "Silo", StringComparison.OrdinalIgnoreCase))
            {
                Game1.player.modData.Remove(FreeSiloCreditModDataKey);
                Helper.GameContent.InvalidateCache("Data/Buildings");
                Monitor.Log("Player built the Silo; clearing the Robin free-silo voucher and reverting Data/Buildings.", LogLevel.Trace);
                return;
            }
        }
    }

    /// Returns a Dinosaur Egg one quality tier above what the player delivered.
    /// Vanilla ladder: 0 regular, 1 silver, 2 gold, 4 iridium (3 is skipped). Iridium stays iridium.
    private void GrantUpgradedDinosaurEgg(StardewValley.Quests.Quest quest)
    {
        int delivered = quest is MoreQuestsItemDeliveryQuest idq ? idq.deliveredQuality.Value : 0;
        int upgraded = delivered switch
        {
            <= 0 => 1,
            1 => 2,
            2 => 4,
            _ => 4
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

    /// True when the player has an active Marnie hay-delivery quest. Matched by title
    /// since Marnie's Cow Offer also asks for Hay (O)178.
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

    /// Builds a Data/Objects entry for one Egg Basket variant. The sprite is a single 16x16
    /// tile, so SpriteIndex 0 covers it. Price is set to 0 because the basket is a quest
    /// reward keepsake, not something the player should farm by replaying.
    private static StardewValley.GameData.Objects.ObjectData BuildEggBasketObject(string displayName, string description, string texture)
    {
        return new StardewValley.GameData.Objects.ObjectData
        {
            Name = displayName,
            DisplayName = displayName,
            Description = description,
            Type = "Crafting",
            Category = -8,
            Price = 0,
            Texture = texture,
            SpriteIndex = 0,
            Edibility = -300,
            IsDrink = false
        };
    }

    private void RegisterContent(IMoreQuestsApi fw)
    {
        var scope = fw.GetModApi(ModManifest);
        ModScope = scope;

        // Custom Quest subclasses. Must register before quests.json loads so PreBuiltQuests
        // of these types round-trip through SpaceCore. AdventureQuest is already registered framework-side.
        scope.RegisterCustomQuestType(typeof(AnySlimeQuest));
        scope.RegisterCustomQuestType(typeof(AnyMonsterQuest));
        scope.RegisterCustomQuestType(typeof(CollectAndReportQuest));

        Generators.RegisterAll(scope);

        // ResolveCooldownTier maps the Short/Medium/Long buckets in quests.json to current
        // ModConfig values, so GMCM edits apply on the next trigger eval without reloading.
        scope.LoadQuestsFromMod(Helper, "assets/quests.json", ResolveCooldownTier);

        ApplyGuildBoardRouting(scope);

        GmcmRegistration.Register(Helper, ModManifest);
    }

    /// Maps a Trigger.CooldownTier name from quests.json to the current ModConfig day count.
    /// Case-insensitive. Unknown tier names fall back to the JSON's CooldownDays literal.
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

    /// Routes mining/monster quests to either the guild board or the help-wanted board
    /// based on EnableAdventurersGuildBoard. When the guild is off, the deep-dive quests
    /// flip back to DailyBoard so the content stays reachable.
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

    /// Applies the current ModConfig tile/offset to the AdventurersGuild board.
    /// Called after board registration and on every GMCM save.
    internal static void ApplyAdventureBoardConfig()
    {
        var board = ModScope?.FindBoard("AdventurersGuild");
        if (board == null)
            return;
        board.Tile = new[] { Config.AdventureBoardTileX, Config.AdventureBoardTileY };
        board.DrawOffset = new[] { Config.AdventureBoardDrawOffsetX, Config.AdventureBoardDrawOffsetY };
    }
}
