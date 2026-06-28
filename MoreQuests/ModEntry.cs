using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuests.Quests;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Content;
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

    /// Watches furniture placements in NPC homes for the Redecorate quest.
    private readonly Quests.FurniturePlacementWatcher _redecorateWatcher = new();

    // Gated diagnostic log. Off by default so the SMAPI log stays quiet in release.
    // Enable via GMCM (Debug logging) when chasing a bug.
    internal static void LogDebug(string message)
    {
        if (Config.DebugLogging && Instance != null)
            Instance.Monitor.Log(message, LogLevel.Trace);
    }

    internal static void LogDebug(Func<string> messageFactory)
    {
        if (Config.DebugLogging && Instance != null)
            Instance.Monitor.Log(messageFactory(), LogLevel.Trace);
    }

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        // Keep rare / endgame items (Qi crops, Prismatic Shard, etc.) out of every crop,
        // flower, and gem objective pool the framework hands out. See QuestItemBlacklist.
        MoreQuestsFramework.ItemResolver.QuestPoolExclusion = QuestItemBlacklist.IsExcluded;

        // Items the player never wants as a reward (always), and, when they opt in, never
        // wants to be asked for in delivery / shipping quests. See RewardExclusionList.
        MoreQuestsFramework.Rewards.RewardApplier.ObjectRewardExclusion = RewardExclusionList.IsExcludedReward;
        MoreQuestsFramework.ItemResolver.RequestItemExclusion = RewardExclusionList.IsExcludedRequest;

        ScanLeahPaintingAssets();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondTick;
        helper.Events.GameLoop.OneSecondUpdateTicked += _redecorateWatcher.OnOneSecondUpdateTicked;
        helper.Events.Player.Warped += _redecorateWatcher.OnWarped;
        helper.Events.Player.Warped += OnPlayerWarped;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        helper.Events.World.BuildingListChanged += OnBuildingListChanged;
        helper.Events.Player.InventoryChanged += MarnieMilkPailHook.OnInventoryChanged;

        var harmony = new Harmony(ModManifest.UniqueID);
        MarniePurchasePatches.Apply(harmony, Monitor);
        JojaBreakInPatches.Apply(harmony, Monitor);
        JojaShelfPatches.Apply(harmony, Helper, Monitor);
    }

    internal const string AdventureBoardAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoard";
    internal const string AdventureBoardBackgroundAssetRoot = "Mods/RafiaBee.MoreQuests/AdventureBoardBackground";

    private const string MqfQuestsAsset = "Mods/RafiaBee.MoreQuestsFramework/Quests";
    private const string MqfBoardsAsset = "Mods/RafiaBee.MoreQuestsFramework/Boards";
    private const string MqfCooldownTiersAsset = "Mods/RafiaBee.MoreQuestsFramework/CooldownTiers";

    private QuestPackDocument? _cachedQuests;
    private BoardPackDocument? _cachedBoards;

    /// Custom step handler id for the final "win the egg hunt" step on Festival.EggHuntSabotage.
    /// The handler isn't registered as a polling handler. We drive it via the framework's
    /// GetActiveCustomSteps from OnDayEnding on Spring 13.
    internal const string EggHuntSabotageStepHandler = "EggHuntSabotage.WinHunt";

    /// Custom step handler ids for Farming.CropCycleQuest. Sow + Water are polled from a
    /// OneSecondUpdateTicked sweep of farm HoeDirt; Harvest is push-driven by the
    /// framework's CropHarvested event. All three dedupe by tile coords via the step's
    /// CreditedKeys (Sow + Water) or by `Loc|x|y|day` (Harvest, to count regrowths).
    internal const string CropCycleSowHandler = "CropCycle.Sow";
    internal const string CropCycleWaterHandler = "CropCycle.Water";
    internal const string CropCycleHarvestHandler = "CropCycle.Harvest";

    /// Custom step handler ids for Story.PierreDontGetCaught. None are polled handlers; each
    /// is driven by a content-side patch or sweep via GetActiveCustomSteps:
    /// - BreakIn: JojaBreakInPatches opens the Joja door in the 12am-2am window and marks it
    ///   done on entry.
    /// - Stock: JojaShelfPatches turns the shelves into deposit boxes for cheap pickles.
    /// - Sign: PollPierreSign watches Town for a stamped rice/wheat pickle sign by the Joja door.
    /// - LayLow: resolved overnight in OnDayEnding once the sign is up.
    internal const string PierreBreakInHandler = "PierreDontGetCaught.BreakIn";
    internal const string PierreStockHandler = "PierreDontGetCaught.Stock";
    internal const string PierreSignHandler = "PierreDontGetCaught.Sign";
    internal const string PierreLayLowHandler = "PierreDontGetCaught.LayLow";

    /// Custom step handler id for Seasonal.BattenDownTheHatches. Polled from the same
    /// OneSecondUpdateTicked sweep, counting lightning rods on the farm. Dedupes by
    /// `Farm|x|y`; rods present when the quest posted are pre-credited in the generator.
    internal const string BattenDownStepHandler = "BattenDown.PlaceRods";
    internal const string BattenDownRewardMailKey = "RafiaBee.MoreQuests.BattenDownReward";

    internal const string EggHuntSabotageRewardMailKey = "RafiaBee.MoreQuests.EggHuntSabotageReward";

    /// Progression flag for the Joja arc. Set on the player the moment Step 1 ("A man of
    /// means") completes; the Step 2 quest ("Quality control") triggers on it (MailReceived
    /// in quests.json). It's a bare flag, not a readable letter, so there's nothing to open.
    internal const string MorrisQualityControlFlag = "RafiaBee.MoreQuests.Morris.QualityControlUnlocked";

    /// Conversation topic for the Joja arc's fallout (Step 3). Switched on for a few days
    /// when Step 2 completes; while it's on, Caroline, Gus, and Evelyn (or Susan with SVE)
    /// each gripe once about the sad produce Pierre stocked. The matching dialogue lines
    /// are injected into their dialogue in OnAssetRequested. Underscores only, since this
    /// doubles as a dialogue key.
    internal const string PierreBadProduceTopic = "RafiaBee_MoreQuests_PierreBadProduce";
    private const int PierreBadProduceTopicDays = 3;

    /// Conversation topic for the fallout of "Don't get caught". Switched on for a few days
    /// when the quest completes; while it's on, Gus and Lewis each remark once that Morris's big
    /// Joja sale turned out to be nothing but pickled wheat and rice (lines injected into their
    /// dialogue in OnAssetRequested). Pierre, who was in on it, instead crows about the sale to
    /// the whole store as a speech bubble on entry (OnPlayerWarped). Underscores only.
    internal const string MorrisCheapskateTopic = "RafiaBee_MoreQuests_MorrisCheapskate";
    private const int MorrisCheapskateTopicDays = 3;

    /// Set on the player while the Cheapskate Morris gossip is running. The morning that topic
    /// expires, the prank "sale" sign outside Joja is cleaned up (the gossip's died down, so the
    /// evidence goes too) and this flag is cleared.
    internal const string PierreSignCleanupKey = "RafiaBee.MoreQuests.PierreSignCleanupPending";

    /// How close to the Joja Mart door (in Town tiles) a stamped pickle sign has to be to
    /// count for the Sign step.
    private const int PierreSignRadius = 8;

    /// ModData flag set on Spring 13 when the player wins the egg hunt while the
    /// Festival.EggHuntSabotage quest is active. Picked up on Spring 14 morning to grant
    /// the iridium-quality egg directly (mail attachments can't carry quality).
    internal const string EggHuntSabotageWonModDataKey = "RafiaBee.MoreQuests.EggHuntSabotageWonThisYear";

    private const string EggHuntSabotageDefinitionId = "Festival.EggHuntSabotage";

    // Vanilla single-player threshold from Event.eggHuntWinner. Stays a constant since
    // the festival data isn't moddable here without rewriting Event.cs.
    private const int EggHuntSingleplayerWinThreshold = 9;

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

    /// Mail key prefix for the Unseen Offering reward letter. Suffix is `{foodId}.{count}`
    /// (count is the final dot-segment so modded ids with dots still parse). The asset edit
    /// reads the suffix and builds the body + food attachment, so nothing is persisted per-quest.
    internal const string UnseenOfferingRewardKeyPrefix = "RafiaBee.MoreQuests.UnseenOfferingReward.";

    /// Mail key prefix for Leah's painting reward. Suffix is the painting id (e.g.
    /// `...LeahPaintingReward.BabyBlueChicken.wood`). The asset edit looks the id up in the
    /// painting pool and attaches the matching furniture. The id is also how the quest dedupes,
    /// since a received key sits in the player's mail history for good.
    internal const string LeahPaintingRewardKeyPrefix = "RafiaBee.MoreQuests.LeahPaintingReward.";

    /// Unqualified furniture id prefix for Leah's paintings. Full id is `<prefix><paintingId>`;
    /// qualified is `(F)<prefix><paintingId>`.
    internal const string LeahPaintingFurnitureIdPrefix = "RafiaBee.MoreQuests.LeahPainting.";

    /// Asset name root for built-in painting textures. Full path is `<root>/<animal>_<frame>`.
    internal const string LeahPaintingTextureRoot = "Mods/RafiaBee.MoreQuests/LeahPainting";

    /// File-system folder (relative to the mod) holding the built-in painting PNGs.
    internal const string LeahPaintingAssetFolder = "assets/leahPaintings";

    /// The painting pool. A `Dictionary<string, LeahPaintingEntry>` keyed by painting id.
    /// MoreQuests loads its built-in paintings here; Content Patcher packs add their own with
    /// an EditData edit. The quest and the Data/Furniture edit both read the merged result.
    internal const string LeahPaintingsDataAsset = "Mods/RafiaBee.MoreQuests/LeahPaintings";

    /// Built-in paintings found by scanning assets/leahPaintings/*.png. Id is `<animal>.<frame>`
    /// (kept dot-separated so saves from before the data-asset change still match); Stem is the
    /// texture file name `<animal>_<frame>`. Feeds the data-asset load factory.
    private readonly List<(string Id, string Animal, string Frame, string Stem)> _builtInPaintings = new();

    /// Texture stems (`<animal>_<frame>`) of the built-in paintings. Validates texture-load
    /// requests under LeahPaintingTextureRoot so we only serve files we actually ship.
    private readonly HashSet<string> _builtInPaintingStems = new(StringComparer.Ordinal);

    /// Lowercase frame keys present in the merged pool. Set at registration from the data asset.
    internal static string[] LeahPaintingFrames { get; private set; } = Array.Empty<string>();

    /// Title-cased frame options shown in GMCM. Derived from LeahPaintingFrames.
    internal static string[] LeahPaintingFrameOptions { get; private set; } = Array.Empty<string>();

    /// Walks assets/leahPaintings for `<Animal>_<frame>.png` filenames and records the built-in
    /// paintings. New PNGs dropped into the folder show up without code changes; other mods add
    /// paintings through the LeahPaintingsDataAsset data asset instead.
    private void ScanLeahPaintingAssets()
    {
        string dir = Path.Combine(Helper.DirectoryPath, LeahPaintingAssetFolder);
        if (!Directory.Exists(dir))
        {
            Monitor.Log($"Leah painting folder missing at '{dir}'.", LogLevel.Warn);
            return;
        }

        foreach (string path in Directory.EnumerateFiles(dir, "*.png"))
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            int sep = stem.LastIndexOf('_');
            if (sep <= 0 || sep == stem.Length - 1)
                continue;
            string animal = stem.Substring(0, sep);
            string frame = stem.Substring(sep + 1).ToLowerInvariant();
            _builtInPaintings.Add(($"{animal}.{frame}", animal, frame, stem));
            _builtInPaintingStems.Add(stem);
        }
    }

    /// Loads the merged painting pool (built-ins plus any Content Patcher additions). SMAPI
    /// caches the asset, so repeated calls are cheap until something invalidates it.
    internal static IReadOnlyDictionary<string, LeahPaintingEntry> GetLeahPaintings()
    {
        try
        {
            return Instance.Helper.GameContent.Load<Dictionary<string, LeahPaintingEntry>>(LeahPaintingsDataAsset);
        }
        catch (Exception ex)
        {
            Instance.Monitor.Log($"Couldn't load the Leah painting pool: {ex.Message}", LogLevel.Warn);
            return new Dictionary<string, LeahPaintingEntry>();
        }
    }

    /// Recomputes the frame list and GMCM options from the merged pool. Run once at
    /// registration (after Content Patcher edits are available) and again whenever the pool is
    /// invalidated, so the config dropdown reflects frames added by other mods.
    private void RefreshLeahPaintingFrames()
    {
        var frames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in GetLeahPaintings().Values)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Frame))
                frames.Add(entry.Frame.Trim().ToLowerInvariant());

        if (frames.Count == 0)
            frames.Add("wood");

        LeahPaintingFrames = frames.ToArray();
        LeahPaintingFrameOptions = LeahPaintingFrames
            .Select(f => char.ToUpperInvariant(f[0]) + f.Substring(1))
            .ToArray();
    }

    internal static string NormalizeLeahPaintingFrame(string? configValue)
    {
        string fallback = LeahPaintingFrames.Length > 0 ? LeahPaintingFrames[0] : "wood";
        if (string.IsNullOrWhiteSpace(configValue))
            return fallback;
        string lower = configValue.Trim().ToLowerInvariant();
        foreach (var f in LeahPaintingFrames)
            if (lower == f) return f;
        return fallback;
    }

    /// Asset edits and loads owned by the content mod: Data/mail reward letters, the
    /// AdventureBoard wall sprite, and its menu background.
    private void OnAssetRequested(object? sender, StardewModdingAPI.Events.AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(MqfQuestsAsset))
        {
            e.Edit(asset =>
            {
                var dict = asset.AsDictionary<string, QuestDef>().Data;
                foreach (var def in LoadQuestsCached().Quests)
                {
                    if (string.IsNullOrWhiteSpace(def.Name))
                        continue;
                    def.Owner = ModManifest.UniqueID;
                    dict[def.Name] = def;
                }
            }, AssetEditPriority.Early);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(MqfBoardsAsset))
        {
            if (!Config.EnableAdventurersGuildBoard)
                return;
            e.Edit(asset =>
            {
                var dict = asset.AsDictionary<string, BoardDefinition>().Data;
                foreach (var def in LoadBoardsCached().Boards)
                {
                    if (string.IsNullOrWhiteSpace(def.Name))
                        continue;
                    def.OwnerUniqueId = ModManifest.UniqueID;
                    dict[def.Name] = def;
                }
            }, AssetEditPriority.Early);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(MqfCooldownTiersAsset))
        {
            e.Edit(asset =>
            {
                var dict = asset.AsDictionary<string, int>().Data;
                dict["Short"] = Config.QuestCooldownShortDays;
                dict["Medium"] = Config.QuestCooldownMediumDays;
                dict["Long"] = Config.QuestCooldownLongDays;
            }, AssetEditPriority.Early);
            return;
        }

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

        if (e.NameWithoutLocale.IsEquivalentTo(LeahPaintingsDataAsset))
        {
            e.LoadFrom(() =>
            {
                var dict = new Dictionary<string, LeahPaintingEntry>(StringComparer.Ordinal);
                foreach (var p in _builtInPaintings)
                {
                    string displayName = I18n.Get($"item.leahPainting.{p.Animal}.name",
                        new { frame = I18n.Get($"item.leahPainting.frame.{p.Frame}").ToString() }).ToString();
                    dict[p.Id] = new LeahPaintingEntry
                    {
                        Texture = $"{LeahPaintingTextureRoot}/{p.Stem}",
                        Frame = p.Frame,
                        DisplayName = displayName
                    };
                }
                return dict;
            }, AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.StartsWith(LeahPaintingTextureRoot + "/"))
        {
            string stem = e.NameWithoutLocale.BaseName.Substring(LeahPaintingTextureRoot.Length + 1);
            if (_builtInPaintingStems.Contains(stem))
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
                foreach (var (id, entry) in GetLeahPaintings())
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Texture))
                        continue;
                    string furnitureId = $"{LeahPaintingFurnitureIdPrefix}{id}";
                    string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                        ? I18n.Get("item.leahPainting.generic.name").ToString()
                        : entry.DisplayName;
                    // Texture path uses backslashes so the '/' field separator in Data/Furniture
                    // doesn't slice the path apart (ArgUtility.Get reads field 9 as the texture).
                    string texture = entry.Texture.Replace('/', '\\');
                    // Format: name/type/tilesheetSize/boundingBox/rotations/price/placement/displayName/spriteIndex/texture
                    // tilesheetSize -1 = default 2x2 for paintings, boundingBox -1 = default 2x2, indoors-only via placement 0.
                    data[furnitureId] = $"{displayName}/painting/-1/-1/1/0/0/{displayName}/0/{texture}";
                }
            }, AssetEditPriority.Default);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/FarmAnimals"))
        {
            if (Game1.player == null)
                return;
            bool hasChicken = Game1.player.modData.ContainsKey(MarniePurchasePatches.ChickenCreditKey);
            bool hasCow = Game1.player.modData.ContainsKey(MarniePurchasePatches.CowCreditKey);
            if (!hasChicken && !hasCow)
                return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, StardewValley.GameData.FarmAnimals.FarmAnimalData>().Data;
                if (hasChicken && data.TryGetValue(MarniePurchasePatches.FreeChickenAnimalType, out var chicken))
                    chicken.PurchasePrice = 0;
                if (hasCow && data.TryGetValue(MarniePurchasePatches.FreeCowAnimalType, out var cow))
                    cow.PurchasePrice = 0;
            }, AssetEditPriority.Late);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Pets"))
        {
            if (Game1.player == null)
                return;
            if (!Game1.player.modData.ContainsKey(MarniePurchasePatches.PetCreditKey))
                return;
            int percent = Math.Clamp(Config.MarniePetDiscountPercent, 1, 100);
            float multiplier = (100 - percent) / 100f;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, StardewValley.GameData.Pets.PetData>().Data;
                foreach (var (_, pet) in data)
                {
                    if (pet?.Breeds == null) continue;
                    foreach (var breed in pet.Breeds)
                    {
                        if (breed == null || !breed.CanBeAdoptedFromMarnie) continue;
                        breed.AdoptionPrice = Math.Max(0, (int)(breed.AdoptionPrice * multiplier));
                    }
                }
            }, AssetEditPriority.Late);
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

        // Joja arc fallout (Step 3): griping about Pierre's produce, keyed to the gossip
        // topic so it only fires while that topic is switched on. The fertilizer wisdom
        // goes to Susan when SVE is installed, otherwise Evelyn.
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/Dialogue/Caroline"))
        {
            e.Edit(asset => asset.AsDictionary<string, string>().Data[PierreBadProduceTopic]
                = I18n.Get("dialogue.pierreBadProduce.caroline").ToString());
            return;
        }
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/Dialogue/Gus"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                data[PierreBadProduceTopic] = I18n.Get("dialogue.pierreBadProduce.gus").ToString();
                data[MorrisCheapskateTopic] = I18n.Get("dialogue.morrisCheapskate.gus").ToString();
            });
            return;
        }
        string fertilizerNpc = MoreQuestsFramework.ModCompat.HasSve(Helper.ModRegistry) ? "Susan" : "Evelyn";
        if (e.NameWithoutLocale.IsEquivalentTo($"Characters/Dialogue/{fertilizerNpc}"))
        {
            e.Edit(asset => asset.AsDictionary<string, string>().Data[PierreBadProduceTopic]
                = I18n.Get("dialogue.pierreBadProduce.fertilizer").ToString());
            return;
        }
        // "Don't get caught" fallout: Gus and Lewis gossip about Morris's sad pickle sale (Gus's
        // line is folded into his block above). Pierre gets no conversation line, he was in on it,
        // so it'd be odd for him to tell the player about it. Instead he crows about the sale to
        // the whole store as a speech bubble when you walk in (OnPlayerWarped).
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/Dialogue/Lewis"))
        {
            e.Edit(asset => asset.AsDictionary<string, string>().Data[MorrisCheapskateTopic]
                = I18n.Get("dialogue.morrisCheapskate.lewis").ToString());
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

            // Battery pack the morning after the storm, charged up by a rod taking a strike.
            string battenDownBody = I18n.Get("mail.seasonal.battenDownReward.body").ToString();
            mail[BattenDownRewardMailKey] = battenDownBody + "%item object 787 1 %%";

            // Thank-you note from the kids the day after winning the egg hunt. No attachment;
            // the iridium egg is granted in code on Spring 14 morning since mail tokens don't
            // carry item quality.
            mail[EggHuntSabotageRewardMailKey] = I18n.Get("mail.festival.eggHuntSabotageReward.body").ToString();

            PopulateDeepDiveRewardLetters(mail);
            PopulateUnseenOfferingRewardLetters(mail);
            PopulateLeahPaintingRewardLetters(mail);
        });
    }

    /// When the painting pool changes (e.g. a Content Patcher pack edits it), rebuild
    /// Data/Furniture so new paintings become placeable, and refresh the frame list so the
    /// config dropdown stays in sync.
    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        foreach (var name in e.NamesWithoutLocale)
        {
            if (name.IsEquivalentTo(LeahPaintingsDataAsset))
            {
                Helper.GameContent.InvalidateCache("Data/Furniture");
                RefreshLeahPaintingFrames();
                break;
            }
        }
    }

    /// Walks the player's mail for Leah painting reward keys and writes one body per key.
    /// Key suffix is the painting id. The body is generic (no animal name) and attaches the
    /// matching `(F)<furnitureId>`, but only when the id is still a known painting.
    private void PopulateLeahPaintingRewardLetters(IDictionary<string, string> mail)
    {
        if (Game1.player == null)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectKeys(Game1.player.mailForTomorrow, seen);
        CollectKeys(Game1.player.mailbox, seen);
        CollectKeys(Game1.player.mailReceived, seen);

        var paintings = GetLeahPaintings();
        string body = I18n.Get("mail.animal.leahFarmPainting.body").ToString();

        foreach (var key in seen)
        {
            if (!key.StartsWith(LeahPaintingRewardKeyPrefix, StringComparison.Ordinal))
                continue;
            string id = key.Substring(LeahPaintingRewardKeyPrefix.Length);
            if (string.IsNullOrEmpty(id))
                continue;

            // Only attach the furniture when the painting still exists (a CP pack that added
            // it could have been removed). The body still lands so the letter isn't broken.
            string attachment = paintings.ContainsKey(id)
                ? $"%item id (F){LeahPaintingFurnitureIdPrefix}{id} %%"
                : string.Empty;
            mail[key] = body + attachment;
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

    /// Scans the player's mail for Unseen Offering reward keys and authors a body + food
    /// attachment for each. Key suffix is `{foodId}.{count}`; count is the final dot-segment
    /// so modded food ids containing dots still parse. Skipped when no save is loaded.
    private void PopulateUnseenOfferingRewardLetters(IDictionary<string, string> mail)
    {
        if (Game1.player == null)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectKeys(Game1.player.mailForTomorrow, seen);
        CollectKeys(Game1.player.mailbox, seen);
        CollectKeys(Game1.player.mailReceived, seen);

        foreach (var key in seen)
        {
            if (!key.StartsWith(UnseenOfferingRewardKeyPrefix, StringComparison.Ordinal))
                continue;
            string suffix = key.Substring(UnseenOfferingRewardKeyPrefix.Length);
            int sep = suffix.LastIndexOf('.');
            if (sep <= 0 || sep == suffix.Length - 1)
                continue;
            string foodId = suffix.Substring(0, sep);
            if (!int.TryParse(suffix.Substring(sep + 1), out int count) || count <= 0)
                continue;

            string foodName;
            try
            {
                foodName = ItemRegistry.GetData("(O)" + foodId)?.DisplayName ?? foodId;
            }
            catch
            {
                foodName = foodId;
            }

            string body = I18n.Get("mail.combat.unseenOfferingReward.body", new { count, food = foodName }).ToString();
            mail[key] = body + $"%item object {foodId} {count} %%";
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
        fw.CropHarvested += OnCropHarvested;

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
        ModEntry.LogDebug("Wired LivestockFollowsYou follower count into the framework's FollowerApiBridge.");
    }

    private void OnQuestRemoved(object? sender, QuestRemovedArgs e)
    {
        if (e.DefinitionId == "Animal.HaySupplyRun")
            Helper.GameContent.InvalidateCache("Data/Shops");

        // Returns the unused budget if a Redecorate quest leaves the log without completing
        // (cancelled or expired). Completion settles itself inside questComplete.
        if (e.Reason != QuestRemovalReason.Completed && e.Quest is Quests.RedecorateQuest rq)
            rq.Settle();
    }

    private static readonly Dictionary<string, Action<ModEntry, StardewValley.Quests.Quest>> QuestCompletionHandlers = new(StringComparer.Ordinal)
    {
        ["Animal.HaySupplyRun"]         = (m, _) => m.Helper.GameContent.InvalidateCache("Data/Shops"),
        ["Story.MorrisManOfMeans"]      = (m, _) => m.UnlockMorrisQualityControl(),
        ["Story.MorrisQualityControl"]  = (m, _) => m.StartPierreBadProduceGossip(),
        ["Story.PierreDontGetCaught"]   = (m, _) => m.StartMorrisCheapskateGossip(),
        ["Animal.GuntherDinosaurStudy"] = (m, q) => m.GrantUpgradedDinosaurEgg(q),
        ["Animal.MarnieChickenOffer"]   = (m, _) => m.GrantMarnieChickenCredit(),
        ["Animal.MarnieCowOffer"]       = (m, _) => m.GrantMarnieCowCredit(),
        ["Animal.RobinSiloOfferCoop"]   = (m, _) => m.GrantFreeSilo(),
        ["Animal.RobinSiloOfferBarn"]   = (m, _) => m.GrantFreeSilo(),
        ["Animal.LeahFarmPainting"]     = (m, _) => m.Helper.GameContent.InvalidateCache("Data/mail"),
        ["Mining.SkullCavernDeepDive"]  = (m, _) => m.Helper.GameContent.InvalidateCache("Data/mail"),
        ["Mining.MinesDeepDive"]        = (m, _) => m.Helper.GameContent.InvalidateCache("Data/mail"),
        ["Mining.UnseenOffering"]       = (m, _) => m.Helper.GameContent.InvalidateCache("Data/mail"),
        ["Foraging.FeedWildCritters"]   = (m, _) => m.OnFeedWildCrittersCompleted(),
        // Belt-and-braces invalidation. The DayEnding resolver already invalidates on
        // the win path, but a completion via any other route still needs the static
        // body to land in the asset cache before vanilla reads it on Spring 14 open.
        [EggHuntSabotageDefinitionId]   = (m, _) => m.Helper.GameContent.InvalidateCache("Data/mail"),
    };

    private void OnQuestCompleted(object? sender, QuestCompletedArgs e)
    {
        if (QuestCompletionHandlers.TryGetValue(e.DefinitionId, out var handler))
            handler(this, e.Quest);
    }

    /// Stamps a "free White Chicken / White Cow at Marnie's shop" credit on the player's
    /// modData. The asset edit on `Data/FarmAnimals` and the `AnimalHouse.adoptAnimal`
    /// postfix in `MarniePurchasePatches` read this. Unredeemed credits expire after
    /// `MarnieCreditExpiryDays` and pay out as gold via `OnDayStarted`.
    private void GrantMarnieChickenCredit() => GrantMarnieCredit(
        MarniePurchasePatches.ChickenCreditKey,
        "quest.animal.marnieChickenOffer.creditIssued");

    private void GrantMarnieCowCredit() => GrantMarnieCredit(
        MarniePurchasePatches.CowCreditKey,
        "quest.animal.marnieCowOffer.creditIssued");

    /// Step 1 of the Joja arc is done, so unlock Step 2. Sets a bare progression flag the
    /// Step 2 quest triggers on (MailReceived), so it posts itself the next morning. No
    /// letter to open, the flag just marks "Morris's real ask is now available."
    private void UnlockMorrisQualityControl()
    {
        if (Game1.player == null)
            return;
        if (!Game1.player.mailReceived.Contains(MorrisQualityControlFlag))
            Game1.player.mailReceived.Add(MorrisQualityControlFlag);
    }

    /// Step 3 of the Joja arc. Step 2 is done, so switch on the town gossip topic for a few
    /// days. Caroline, Gus, and Evelyn (or Susan with SVE) each have a line keyed to this
    /// topic, so they grumble about Pierre's produce next time you chat with them. The
    /// player did the sabotage in secret, so nobody pins it on them.
    private void StartPierreBadProduceGossip()
    {
        if (Game1.player == null)
            return;
        Game1.player.activeDialogueEvents[PierreBadProduceTopic] = PierreBadProduceTopicDays;
    }

    /// Fallout of "Don't get caught". The job's done, so switch on the town gossip about
    /// Morris's sad pickle sale for a few days (Gus and Lewis have lines keyed to the topic;
    /// Pierre crows about it in his shop instead). Also flags the prank sign for cleanup once
    /// the gossip ends. Nobody knows the player did it.
    private void StartMorrisCheapskateGossip()
    {
        if (Game1.player == null)
            return;
        Game1.player.activeDialogueEvents[MorrisCheapskateTopic] = MorrisCheapskateTopicDays;
        Game1.player.modData[PierreSignCleanupKey] = "1";
    }

    /// Once the Cheapskate Morris topic has run its course, take down the prank "sale" sign
    /// outside Joja. Driven off the topic actually being gone rather than a counted day, so it
    /// lines up with the gossip ending no matter how the day count lands. Stateless beyond the
    /// pending flag, so it survives a save/reload mid-gossip.
    private void ProcessPierreSignCleanup(Farmer player)
    {
        if (!player.modData.ContainsKey(PierreSignCleanupKey))
            return;
        if (player.activeDialogueEvents.ContainsKey(MorrisCheapskateTopic))
            return;
        RemovePierreSign();
        player.modData.Remove(PierreSignCleanupKey);
    }

    private void RemovePierreSign()
    {
        var town = Game1.getLocationFromName("Town");
        if (town?.Objects == null)
            return;
        var door = FindJojaDoorTile(town);
        if (!door.HasValue)
            return;

        Microsoft.Xna.Framework.Vector2? signTile = null;
        foreach (var pair in town.Objects.Pairs)
        {
            if (pair.Value is not StardewValley.Objects.Sign sign)
                continue;
            if (Math.Abs((int)pair.Key.X - door.Value.X) > PierreSignRadius
                || Math.Abs((int)pair.Key.Y - door.Value.Y) > PierreSignRadius)
                continue;
            if (!JojaShelfPatches.IsCheapPickle(sign.displayItem.Value))
                continue;
            signTile = pair.Key;
            break;
        }
        if (signTile.HasValue)
            town.Objects.Remove(signTile.Value);
    }

    private int _pierreSaleBubbleDay = -1;

    /// While the Cheapskate Morris topic is on, Pierre crows about his "fresh and local" sale to
    /// the whole store the moment you step into the SeedShop, an overhead speech bubble rather
    /// than a one-on-one line, so he's playing it up for the room and never lets on he set it up.
    /// Once per day so re-entering doesn't spam it.
    private void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady || !e.IsLocalPlayer || e.NewLocation == null)
            return;
        if (e.NewLocation.Name != "SeedShop" || Game1.player == null)
            return;
        if (!Game1.player.activeDialogueEvents.ContainsKey(MorrisCheapskateTopic))
            return;
        if (_pierreSaleBubbleDay == Game1.Date.TotalDays)
            return;

        NPC? pierre = null;
        foreach (var npc in e.NewLocation.characters)
        {
            if (npc?.Name == "Pierre")
            {
                pierre = npc;
                break;
            }
        }
        if (pierre == null)
            return;

        _pierreSaleBubbleDay = Game1.Date.TotalDays;
        pierre.showTextAboveHead(
            I18n.Get("quest.story.pierreDontGetCaught.pierreSaleBubble").ToString(),
            null, 2, 4000);
    }

    /// Place-the-sign step of "Don't get caught". It only goes active after the framework Craft
    /// step (CraftSign) is done, so the player has to have actually crafted a sign first. Here we
    /// just watch Town for a sign near the Joja door with a cheap rice/wheat pickle stamped onto
    /// it (vanilla hold-pickle-then-right-click-sign sets the sign's displayItem).
    private void PollPierreSign()
    {
        if (ModScope == null || Game1.player == null)
            return;
        var steps = ModScope.GetActiveCustomSteps(PierreSignHandler);
        if (steps.Count == 0)
            return;
        var town = Game1.getLocationFromName("Town");
        if (town?.Objects == null)
            return;
        var door = FindJojaDoorTile(town);
        if (!door.HasValue)
            return;

        foreach (var pair in town.Objects.Pairs)
        {
            if (pair.Value is not StardewValley.Objects.Sign sign)
                continue;
            if (Math.Abs((int)pair.Key.X - door.Value.X) > PierreSignRadius
                || Math.Abs((int)pair.Key.Y - door.Value.Y) > PierreSignRadius)
                continue;
            if (!JojaShelfPatches.IsCheapPickle(sign.displayItem.Value))
                continue;
            foreach (var handle in steps)
                if (handle.IsActive)
                    handle.MarkDone();
            return;
        }
    }

    /// LayLow step of "Don't get caught": go home and sleep on it. The step only goes active
    /// after the sign is up (it Requires Sign), so any sleep at that point finishes the quest,
    /// which fires the reward and the Cheapskate Morris gossip.
    private void ResolvePierreLayLow()
    {
        if (ModScope == null)
            return;
        foreach (var handle in ModScope.GetActiveCustomSteps(PierreLayLowHandler))
            if (handle.IsActive)
                handle.MarkDone();
    }

    /// The Joja Mart door tile in Town, found once by scanning for the warp Action that points
    /// at JojaMart (so it tracks map edits instead of a hardcoded coordinate). Cached.
    private static Microsoft.Xna.Framework.Point? _jojaDoorTile;

    private static Microsoft.Xna.Framework.Point? FindJojaDoorTile(GameLocation town)
    {
        if (_jojaDoorTile.HasValue)
            return _jojaDoorTile;
        var buildings = town.Map?.GetLayer("Buildings");
        if (buildings == null)
            return null;
        for (int x = 0; x < buildings.LayerWidth; x++)
        {
            for (int y = 0; y < buildings.LayerHeight; y++)
            {
                var tile = buildings.Tiles[x, y];
                if (tile == null)
                    continue;
                if (tile.Properties.TryGetValue("Action", out var val) && val != null
                    && val.ToString().IndexOf("JojaMart", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _jojaDoorTile = new Microsoft.Xna.Framework.Point(x, y);
                    return _jojaDoorTile;
                }
            }
        }
        return null;
    }

    private void GrantMarnieCredit(string modDataKey, string hudKey)
    {
        var player = Game1.player;
        if (player == null)
            return;
        int days = Math.Max(1, Config.MarnieCreditExpiryDays);
        uint expiry = Game1.stats.DaysPlayed + (uint)days;
        player.modData[modDataKey] = expiry.ToString();
        Helper.GameContent.InvalidateCache("Data/FarmAnimals");
        Game1.addHUDMessage(new HUDMessage(
            I18n.Get(hudKey, new { days }).ToString(),
            HUDMessage.newQuest_type));
    }

    /// Polls every active Crop Cycle Sow and Water step once a second. Cheaper than a
    /// Harmony patch on HoeDirt because sprinklers + rain + Junimo activity all set
    /// state.Value transparently, so a single tile-scan picks every path up. Dedupes
    /// per-tile via the step's CreditedKeys so re-watering a tile next day doesn't
    /// re-credit, and the scan stops early once a step's quota fills.
    private void OnOneSecondTick(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (Game1.player == null || ModScope == null)
            return;

        PollBattenDownRods();
        PollPierreSign();

        var sow = ModScope.GetActiveCustomSteps(CropCycleSowHandler);
        var water = ModScope.GetActiveCustomSteps(CropCycleWaterHandler);
        if (sow.Count == 0 && water.Count == 0)
            return;

        foreach (var location in Game1.locations)
        {
            if (location?.terrainFeatures == null || location.terrainFeatures.Count() == 0)
                continue;
            foreach (var pair in location.terrainFeatures.Pairs)
            {
                var feature = pair.Value;
                if (feature is not StardewValley.TerrainFeatures.HoeDirt hd)
                    continue;
                if (hd.crop == null)
                    continue;
                string harvestId = hd.crop.indexOfHarvest.Value ?? string.Empty;
                if (string.IsNullOrEmpty(harvestId))
                    continue;
                int tileX = (int)pair.Key.X;
                int tileY = (int)pair.Key.Y;
                string locName = location.NameOrUniqueName ?? string.Empty;
                string tileKey = locName + "|" + tileX + "|" + tileY;

                if (sow.Count > 0)
                {
                    foreach (var handle in sow)
                    {
                        if (!handle.IsActive) continue;
                        if (!CropCycleStepMatchesHarvestId(handle, harvestId, sowStep: true)) continue;
                        handle.AddProgressOnceForKey(tileKey);
                    }
                }
                if (water.Count > 0 && hd.state.Value == 1)
                {
                    foreach (var handle in water)
                    {
                        if (!handle.IsActive) continue;
                        if (!CropCycleStepMatchesHarvestId(handle, harvestId, sowStep: false)) continue;
                        handle.AddProgressOnceForKey(tileKey);
                    }
                }
            }
        }
    }

    /// Credits Seasonal.BattenDownTheHatches for each lightning rod standing on the farm.
    /// Dedupes by tile (`Farm|x|y`) so picking a rod up and dropping it back never re-credits,
    /// and the generator pre-seeds existing rods into CreditedKeys so only new placements count.
    private void PollBattenDownRods()
    {
        var steps = ModScope!.GetActiveCustomSteps(BattenDownStepHandler);
        if (steps.Count == 0)
            return;

        var farm = Game1.getFarm();
        if (farm?.Objects == null)
            return;

        foreach (var pair in farm.Objects.Pairs)
        {
            if (pair.Value?.QualifiedItemId != "(BC)9")
                continue;
            string tileKey = $"Farm|{(int)pair.Key.X}|{(int)pair.Key.Y}";
            foreach (var handle in steps)
            {
                if (!handle.IsActive)
                    continue;
                handle.AddProgressOnceForKey(tileKey);
            }
        }
    }

    /// Sow step's Items[] is [seedId, cropQualifiedId]; the crop's indexOfHarvest matches
    /// the bare cropId. Water step's Items[] leads with cropQualifiedId. Bare-id comparison
    /// covers both qualified and unqualified entries.
    private static bool CropCycleStepMatchesHarvestId(MoreQuestsFramework.Quests.ICustomStepHandle handle, string harvestBareId, bool sowStep)
    {
        var items = handle.Items;
        if (items == null || items.Count == 0)
            return false;
        int cropIdx = sowStep && items.Count >= 2 ? 1 : 0;
        string raw = items[cropIdx] ?? string.Empty;
        if (string.IsNullOrEmpty(raw))
            return false;
        string bare = raw.StartsWith("(O)", StringComparison.Ordinal) ? raw.Substring(3) : raw;
        return string.Equals(bare, harvestBareId, StringComparison.OrdinalIgnoreCase);
    }

    /// Crop Cycle's Harvest step is push-driven by the framework's CropHarvested event.
    /// Each harvest is one credit per matching active step. Dedupes by `Loc|x|y|day` so a
    /// regrowth on day 2 of the same tile counts as a separate harvest event. Player and
    /// Junimo harvests both fire the event.
    private void OnCropHarvested(object? sender, MoreQuestsFramework.Api.CropHarvestInfo info)
    {
        if (ModScope == null)
            return;
        var handles = ModScope.GetActiveCustomSteps(CropCycleHarvestHandler);
        if (handles.Count == 0)
            return;

        string bareHarvest = info.CropQualifiedId.StartsWith("(O)", StringComparison.Ordinal)
            ? info.CropQualifiedId.Substring(3)
            : info.CropQualifiedId;
        string dedupeKey = info.LocationName + "|" + info.TileX + "|" + info.TileY + "|" + Game1.Date.TotalDays;

        foreach (var handle in handles)
        {
            if (!handle.IsActive) continue;
            if (!CropCycleStepMatchesHarvestId(handle, bareHarvest, sowStep: false)) continue;
            handle.AddProgressOnceForKey(dedupeKey);
        }
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        var player = Game1.player;
        if (player == null)
            return;
        TryExpireCredit(player, MarniePurchasePatches.ChickenCreditKey, Config.MarnieChickenOfferRebate, "quest.animal.marnieChickenOffer.creditExpired");
        TryExpireCredit(player, MarniePurchasePatches.CowCreditKey, Config.MarnieCowOfferRebate, "quest.animal.marnieCowOffer.creditExpired");
        TryExpirePetCredit(player);

        PushEggHuntSabotageChildLines();
        TryGrantEggHuntSabotageEgg();
        ProcessPierreSignCleanup(player);
    }

    /// Spring 10-13, when Festival.EggHuntSabotage is active, push a randomly-picked
    /// "plan" line onto each met child's CurrentDialogue (Vincent excluded since he's
    /// the giver). Pure flavor: no quest step advances from these lines. Vanilla clears
    /// CurrentDialogue on day rollover, so re-pushing every morning doesn't stack.
    /// Each kid picks independently so the lines vary across the group; reseeded each
    /// day via the current date so the picks are stable within a save day.
    private void PushEggHuntSabotageChildLines()
    {
        if (!IsEggHuntSabotageDialogueWindow())
            return;
        if (!IsEggHuntSabotageQuestActive())
            return;

        foreach (var name in MetChildHumanGivers(Game1.player))
        {
            if (string.Equals(name, "Vincent", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!MoreQuestsFramework.NpcDisplay.IsBoardEligible(name, allowChild: true))
                continue;
            var npc = Game1.getCharacterFromName(name);
            if (npc == null) continue;
            int variant = Math.Abs(StringComparer.Ordinal.GetHashCode(name) ^ Game1.Date.TotalDays) % EggHuntSabotageDialogueVariants;
            string text = I18n.Get($"quest.festival.eggHuntSabotage.dialogue.kid{variant + 1}", new { npc = npc.displayName }).ToString();
            npc.CurrentDialogue.Push(new StardewValley.Dialogue(npc, null, text));
        }
    }

    private const int EggHuntSabotageDialogueVariants = 5;

    private static bool IsEggHuntSabotageDialogueWindow()
    {
        return string.Equals(Game1.currentSeason, "spring", StringComparison.Ordinal)
               && Game1.dayOfMonth >= 10 && Game1.dayOfMonth <= 13;
    }

    /// On Spring 14 morning, if the win flag is set, hand the iridium egg over with a
    /// HUDMessage. Done in code because mail attachments don't carry item quality.
    private void TryGrantEggHuntSabotageEgg()
    {
        var player = Game1.player;
        if (player == null) return;
        if (!player.modData.TryGetValue(EggHuntSabotageWonModDataKey, out _))
            return;
        player.modData.Remove(EggHuntSabotageWonModDataKey);

        var egg = StardewValley.ItemRegistry.Create<StardewValley.Object>("(O)176", 1, 4);
        if (egg == null)
            return;
        Game1.player.addItemByMenuIfNecessary(egg);
        Game1.addHUDMessage(new HUDMessage(
            I18n.Get("quest.festival.eggHuntSabotage.hud.eggDelivered").ToString(),
            HUDMessage.achievement_type));
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        ResolveEggHuntSabotageOutcome();
        ResolvePierreLayLow();
    }

    /// On Spring 13 evening, resolve any active Festival.EggHuntSabotage quest by the
    /// player's festivalScore (vanilla's single-player win threshold is 9). Win: mark the
    /// Custom Win step done via the framework handle so the standard reward pipeline fires
    /// (mail + friendship), and stamp a modData flag so the iridium egg is granted on the
    /// next morning. Loss: -FriendshipMultiSmall to every met child and quietly remove
    /// the quest from the log.
    private void ResolveEggHuntSabotageOutcome()
    {
        if (!string.Equals(Game1.currentSeason, "spring", StringComparison.Ordinal)) return;
        if (Game1.dayOfMonth != 13) return;
        var player = Game1.player;
        if (player == null || ModScope == null) return;

        // GetActiveCustomSteps walks the questLog directly looking for our handler id, so
        // it survives save/load. Framework.GetDefinitionId uses a weak table that doesn't.
        var handles = ModScope.GetActiveCustomSteps(EggHuntSabotageStepHandler);
        if (handles.Count == 0) return;
        var handle = handles[0];
        var quest = handle.Quest;

        Monitor.Log($"EggHuntSabotage: resolving on Spring 13 evening. festivalScore={player.festivalScore}, threshold={EggHuntSingleplayerWinThreshold}.", LogLevel.Info);

        if (player.festivalScore >= EggHuntSingleplayerWinThreshold)
        {
            bool marked = handle.MarkDone();
            Monitor.Log($"EggHuntSabotage: MarkDone returned {marked}. quest.completed={quest.completed.Value}.", LogLevel.Info);
            if (!marked)
                Monitor.Log("EggHuntSabotage: win condition met but MarkDone failed; rewards may not have applied. Bug.", LogLevel.Warn);
            else
                Helper.GameContent.InvalidateCache("Data/mail");
            player.modData[EggHuntSabotageWonModDataKey] = "true";
        }
        else
        {
            const int penalty = -30;
            foreach (var kid in MetChildHumanGivers(player))
            {
                if (!MoreQuestsFramework.NpcDisplay.IsBoardEligible(kid, allowChild: true))
                    continue;
                var npc = Game1.getCharacterFromName(kid);
                if (npc == null) continue;
                player.changeFriendship(penalty, npc);
            }
            player.questLog.Remove(quest);
            Game1.addHUDMessage(new HUDMessage(
                I18n.Get("quest.festival.eggHuntSabotage.hud.lost").ToString(),
                HUDMessage.error_type));
            Monitor.Log("EggHuntSabotage: applied loss path (kids -30 friendship, quest removed).", LogLevel.Info);
        }
    }

    /// True when a Festival.EggHuntSabotage quest is currently in the player's journal.
    /// Looks for the Custom Win step's handler id so the check survives save/load (the
    /// framework's GetDefinitionId weak table doesn't).
    private bool IsEggHuntSabotageQuestActive()
    {
        if (ModScope == null) return false;
        return ModScope.GetActiveCustomSteps(EggHuntSabotageStepHandler).Count > 0;
    }

    private static List<string> MetChildHumanGivers(Farmer player)
    {
        var result = new List<string>();
        foreach (var (name, _) in player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null || npc.IsMonster || !npc.IsVillager) continue;
            var data = npc.GetData();
            if (data == null || data.Age != StardewValley.GameData.Characters.NpcAge.Child) continue;
            if (!player.friendshipData.TryGetValue(name, out var friendship) || friendship == null || friendship.Points <= 0)
                continue;
            result.Add(name);
        }
        return result;
    }

    private void TryExpirePetCredit(Farmer player)
    {
        if (!player.modData.TryGetValue(MarniePurchasePatches.PetCreditKey, out string? raw))
            return;
        if (!uint.TryParse(raw, out uint expiry))
        {
            player.modData.Remove(MarniePurchasePatches.PetCreditKey);
            return;
        }
        if (Game1.stats.DaysPlayed < expiry)
            return;
        player.modData.Remove(MarniePurchasePatches.PetCreditKey);
        Helper.GameContent.InvalidateCache("Data/Pets");
        Game1.addHUDMessage(new HUDMessage(
            I18n.Get("quest.foraging.feedWildCritters.creditExpired").ToString(),
            HUDMessage.achievement_type));
    }

    /// Persists across saves on `Game1.player.modData`. Bumped when Foraging.FeedWildCritters
    /// completes; on the Nth completion (Config.FeedWildCrittersPerPetCredit) Marnie issues
    /// the pet discount credit and the counter resets to 0.
    internal const string WildCrittersFedCountKey = "RafiaBee.MoreQuests.WildCrittersFedCount";

    private void OnFeedWildCrittersCompleted()
    {
        var player = Game1.player;
        if (player == null)
            return;
        int required = Math.Max(1, Config.FeedWildCrittersPerPetCredit);
        int count = 0;
        if (player.modData.TryGetValue(WildCrittersFedCountKey, out string? raw))
            int.TryParse(raw, out count);
        count++;
        if (count >= required)
        {
            player.modData.Remove(WildCrittersFedCountKey);
            GrantMarniePetCredit();
        }
        else
        {
            player.modData[WildCrittersFedCountKey] = count.ToString();
        }
    }

    private void GrantMarniePetCredit()
    {
        var player = Game1.player;
        if (player == null)
            return;
        int days = Math.Max(1, Config.MarniePetCreditExpiryDays);
        int percent = Math.Clamp(Config.MarniePetDiscountPercent, 1, 100);
        uint expiry = Game1.stats.DaysPlayed + (uint)days;
        player.modData[MarniePurchasePatches.PetCreditKey] = expiry.ToString();
        Helper.GameContent.InvalidateCache("Data/Pets");
        Game1.addHUDMessage(new HUDMessage(
            I18n.Get("quest.foraging.feedWildCritters.creditIssued", new { percent, days }).ToString(),
            HUDMessage.newQuest_type));
    }

    private void TryExpireCredit(Farmer player, string key, int rebateAmount, string hudKey)
    {
        if (!player.modData.TryGetValue(key, out string? raw))
            return;
        if (!uint.TryParse(raw, out uint expiry))
        {
            player.modData.Remove(key);
            return;
        }
        if (Game1.stats.DaysPlayed < expiry)
            return;

        player.modData.Remove(key);
        Helper.GameContent.InvalidateCache("Data/FarmAnimals");
        int rebate = Math.Max(0, rebateAmount);
        if (rebate > 0)
        {
            player.Money += rebate;
            Game1.addHUDMessage(new HUDMessage(
                I18n.Get(hudKey, new { gold = rebate }).ToString(),
                HUDMessage.achievement_type));
        }
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
                    ModEntry.LogDebug("Player already has a Silo by quest completion; skipping free-silo voucher.");
                    return;
                }
            }
        }

        Game1.player.modData[FreeSiloCreditModDataKey] = "true";
        Helper.GameContent.InvalidateCache("Data/Buildings");
        Game1.addHUDMessage(new StardewValley.HUDMessage(I18n.Get("quest.animal.robinSilo.voucher").ToString(), 1));
        ModEntry.LogDebug("Set the Robin free-silo voucher; Silo in Robin's carpenter menu will cost 0g and no materials until built.");
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
                ModEntry.LogDebug("Player built the Silo; clearing the Robin free-silo voucher and reverting Data/Buildings.");
                return;
            }
        }
    }

    /// Returns a Dinosaur Egg one quality tier above what the player delivered.
    /// Vanilla ladder: 0 regular, 1 silver, 2 gold, 4 iridium (3 is skipped). Iridium stays iridium.
    private void GrantUpgradedDinosaurEgg(StardewValley.Quests.Quest quest)
    {
        int delivered = Framework?.GetDeliveredQuality(quest) ?? 0;
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
        // Pays the budget the moment a Redecorate quest lands in the log. GrantBudgetOnAccept
        // is guarded so the reload-time re-fire of QuestAccepted can't pay twice.
        if (e.Quest is Quests.RedecorateQuest rq)
            rq.GrantBudgetOnAccept();

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
        ModEntry.LogDebug($"Granted '{recipe}' crafting recipe for Ridgeside Gathering quest.");
    }

    /// True when the player has an active Marnie hay-delivery quest. Matched by the
    /// framework's definition id, not the translated title (Marnie's Cow Offer also
    /// asks for Hay (O)178, so the items overlap and the title is a translation pack
    /// away from drifting).
    private bool IsHaySupplyRunActive()
    {
        var log = Game1.player?.questLog;
        if (log == null || log.Count == 0 || Framework == null)
            return false;
        foreach (var q in log)
        {
            if (q == null || q.completed.Value)
                continue;
            if (Framework.GetDefinitionId(q) == "Animal.HaySupplyRun")
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
        scope.RegisterCustomQuestType(typeof(PurchaseFromShopQuest));
        scope.RegisterCustomQuestType(typeof(RedecorateQuest));

        // Mail-stash codecs for the two subclasses MarnieCowOffer can post via mail.
        // Without these, a save+reload before opening the letter loses the quest.
        scope.RegisterMailStashCodec(
            PurchaseFromShopQuestStashCodec.Kind,
            typeof(PurchaseFromShopQuest),
            PurchaseFromShopQuestStashCodec.Encode,
            PurchaseFromShopQuestStashCodec.Decode);
        scope.RegisterMailStashCodec(
            CollectAndReportQuestStashCodec.Kind,
            typeof(CollectAndReportQuest),
            CollectAndReportQuestStashCodec.Encode,
            CollectAndReportQuestStashCodec.Decode);

        Generators.RegisterAll(scope);

        // The Redecorate quest is a self-contained IQuestDefinition (not a quests.json
        // generator). It only posts when Build Placement Unlocker is installed; see
        // RedecorateQuestDefinition.IsAvailable.
        scope.RegisterQuest(new RedecorateQuestDefinition());

        ApplyGuildBoardRouting(scope);

        // Pull the frame options from the merged painting pool (built-ins + any Content Patcher
        // additions) before GMCM reads them, so the dropdown lists every available frame.
        RefreshLeahPaintingFrames();
        GmcmRegistration.Register(Helper, ModManifest);
    }

    private QuestPackDocument LoadQuestsCached()
    {
        return _cachedQuests ??= Helper.Data.ReadJsonFile<QuestPackDocument>("assets/quests.json") ?? new QuestPackDocument();
    }

    private BoardPackDocument LoadBoardsCached()
    {
        return _cachedBoards ??= Helper.Data.ReadJsonFile<BoardPackDocument>("assets/boards.json") ?? new BoardPackDocument();
    }

    /// Routes mining/monster quests to either the guild board or the help-wanted board
    /// based on EnableAdventurersGuildBoard. Every Mining-category quest except
    /// Mining.BarDelivery defaults to CustomBoard in quests.json; when the guild is off
    /// they flip back to DailyBoard so the content stays reachable. Vanilla.SlayMonster
    /// is defined in code with a DailyBoard default, so it gets overridden the other way
    /// when the guild is on.
    private void ApplyGuildBoardRouting(IMoreQuestsModApi scope)
    {
        if (Config.EnableAdventurersGuildBoard)
        {
            scope.OverrideTriggerSource("Vanilla.SlayMonster", TriggerSource.CustomBoard, "AdventurersGuild");
            ApplyAdventureBoardConfig();
        }
        else
        {
            scope.OverrideTriggerSource("Mining.BasicSlimeClearing", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.MonsterHunt", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.MonsterParts", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.RareMaterialRequest", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.SkullCavernDeepDive", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.MinesDeepDive", TriggerSource.DailyBoard);
            scope.OverrideTriggerSource("Mining.UnseenOffering", TriggerSource.DailyBoard);
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
    }
}
