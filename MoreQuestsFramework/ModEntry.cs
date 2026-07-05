using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Patches;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Posting.Boards;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Quests.Vanilla;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.State;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    // Getter is public so consumer mods (MoreQuests) can read the framework's reward /
    // friendship tiers from event handlers that don't carry a QuestContext. Same object
    // QuestContext.Config already exposes; writes stay in-framework.
    public static MoreQuestsFrameworkConfig Config { get; internal set; } = new();
    internal static ITranslationHelper? Translation { get; private set; }

    internal const string PadAssetRoot = "Mods/RafiaBee.MoreQuestsFramework/Pad";
    internal const string PinAssetRoot = "Mods/RafiaBee.MoreQuestsFramework/Pin";
    internal const string QuestsAssetName = "Mods/RafiaBee.MoreQuestsFramework/Quests";
    internal const string BoardsAssetName = "Mods/RafiaBee.MoreQuestsFramework/Boards";
    internal const string NoticesAssetName = "Mods/RafiaBee.MoreQuestsFramework/Notices";
    internal const string CooldownTiersAssetName = "Mods/RafiaBee.MoreQuestsFramework/CooldownTiers";
    internal const string CategoriesAssetName = "Mods/RafiaBee.MoreQuestsFramework/Categories";

    // Parsed pad/pin colors + skill per category. Seeded with the built-ins so it's safe
    // to read before the asset loads; rebuilt on asset load and invalidation.
    internal static CategoryRegistry Categories { get; private set; } = null!;

    // Categories registered by C# consumers via RegisterCategory, merged into the
    // Categories asset's seed so they survive invalidation like the built-ins.
    internal static readonly Dictionary<string, CategoryDefinition> RegisteredCategories =
        new(StringComparer.OrdinalIgnoreCase);

    private QuestRegistry _registry = null!;
    private GeneratorRegistry _generators = null!;
    private CustomStepRegistry _customSteps = null!;
    private ReportBackRegistry _reportBack = null!;
    private CustomTriggerRegistry _customTriggers = null!;
    private CustomRewardRegistry _customRewards = null!;
    private CustomConditionRegistry _customConditions = null!;
    private CustomBoardQuestRegistry _customBoardQuests = null!;
    private QuestPackLoader _loader = null!;
    private BoardRegistry _boards = null!;
    private BoardPackLoader _boardLoader = null!;
    private NoticeRegistry _notices = null!;
    private NoticePackLoader _noticeLoader = null!;
    private NoticeStore? _noticeStore;
    private Func<string, int?>? _cooldownTierLookup;
    private MailStashCodecRegistry _mailStashCodecs = null!;
    private BoardWorldRenderer? _boardRenderer;
    private QuestPipeline? _pipeline;
    private QuestPoster? _poster;
    private GameDataCache? _dataCache;
    private AntiRepetition? _antiRepetition;
    private QuestContext? _ctx;
    private MoreQuestsApi _api = null!;
    private StateStore? _stateStore;
    private TriggerEvaluator? _triggers;
    private DialogueWatcher? _dialogueWatcher;
    private MailQuestRegistry _mailQuests = null!;
    private SpecialOrderWriter? _specialOrderWriter;
    private ShopDiscountWriter? _shopDiscountWriter;
    private AnimalPurchaseDiscountWriter? _animalPurchaseDiscountWriter;
    private FestivalBiasWriter? _festivalBiasWriter;
    private FairStarTokensWriter? _fairStarTokensWriter;
    private bool _fairTokensAppliedThisSession;
    private ConsequenceEngine? _consequenceEngine;
    private ConsequenceDialogueWatcher? _consequenceWatcher;
    private ReportBackWatcher? _reportBackWatcher;

    internal DispatchRegistry Dispatch { get; private set; } = null!;
    internal CombatFoodRegistry CombatFood { get; private set; } = null!;
    internal MoreQuestsApi Api => _api;
    internal AntiRepetition? Anti => _antiRepetition;

    private readonly HashSet<Quest> _watching = new();
    // Value is "was the quest timed when we first saw it in the log", used at removal
    // time to tell expiration apart from a player cancel (both leave completed=false,
    // but only an expiration zeros out daysLeft from a previously-positive value).
    private readonly Dictionary<Quest, bool> _seenInLog = new();
    private readonly HashSet<Quest> _completedFired = new();
    private readonly HashSet<Quest> _seenThisTick = new();

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Translation = helper.Translation;
        Config = helper.ReadConfig<MoreQuestsFrameworkConfig>();

        Categories = new CategoryRegistry(Monitor);

        _registry = new QuestRegistry(Monitor);
        _generators = new GeneratorRegistry(Monitor);
        _customSteps = new CustomStepRegistry(Monitor);
        _reportBack = new ReportBackRegistry(Monitor);
        _customTriggers = new CustomTriggerRegistry(Monitor);
        _customRewards = new CustomRewardRegistry(Monitor);
        RewardApplier.CustomRewards = _customRewards;
        _customConditions = new CustomConditionRegistry(Monitor);
        ConditionEvaluator.CustomConditions = _customConditions;
        ConditionEvaluator.Monitor = Monitor;
        _customBoardQuests = new CustomBoardQuestRegistry(Monitor);
        QuestFactory.CustomBoardQuests = _customBoardQuests;
        _loader = new QuestPackLoader(_registry, _generators, Monitor);
        _boards = new BoardRegistry(Monitor);
        _boardLoader = new BoardPackLoader(_boards, Monitor);
        _notices = new NoticeRegistry(Monitor);
        _noticeLoader = new NoticePackLoader(_notices, Monitor);
        Dispatch = new DispatchRegistry(helper.ModRegistry, Monitor);
        CombatFood = new CombatFoodRegistry(Monitor);
        _mailStashCodecs = new MailStashCodecRegistry(Monitor);
        _mailStashCodecs.Register(AdventureQuestStashCodec.Kind, typeof(AdventureQuest), AdventureQuestStashCodec.Encode, AdventureQuestStashCodec.Decode);
        _mailStashCodecs.Register(MoreQuestsShipQuestStashCodec.Kind, typeof(MoreQuestsShipQuest), MoreQuestsShipQuestStashCodec.Encode, MoreQuestsShipQuestStashCodec.Decode);
        _mailStashCodecs.Register(MoreQuestsEarnMoneyQuestStashCodec.Kind, typeof(MoreQuestsEarnMoneyQuest), MoreQuestsEarnMoneyQuestStashCodec.Encode, MoreQuestsEarnMoneyQuestStashCodec.Decode);
        _mailStashCodecs.Register(MoreQuestsSellQuestStashCodec.Kind, typeof(MoreQuestsSellQuest), MoreQuestsSellQuestStashCodec.Encode, MoreQuestsSellQuestStashCodec.Decode);
        _mailStashCodecs.Register(VanillaItemDeliveryQuestStashCodec.Kind, typeof(StardewValley.Quests.ItemDeliveryQuest), VanillaItemDeliveryQuestStashCodec.Encode, VanillaItemDeliveryQuestStashCodec.Decode);
        _mailStashCodecs.Register(VanillaFishingQuestStashCodec.Kind, typeof(StardewValley.Quests.FishingQuest), VanillaFishingQuestStashCodec.Encode, VanillaFishingQuestStashCodec.Decode);
        _mailStashCodecs.Register(VanillaSlayMonsterQuestStashCodec.Kind, typeof(StardewValley.Quests.SlayMonsterQuest), VanillaSlayMonsterQuestStashCodec.Encode, VanillaSlayMonsterQuestStashCodec.Decode);
        _mailStashCodecs.Register(VanillaResourceCollectionQuestStashCodec.Kind, typeof(StardewValley.Quests.ResourceCollectionQuest), VanillaResourceCollectionQuestStashCodec.Encode, VanillaResourceCollectionQuestStashCodec.Decode);
        _api = new MoreQuestsApi(_registry, _generators, _customSteps, _reportBack, _customTriggers, _customRewards, _customConditions, _customBoardQuests, Dispatch, _boards, _notices, CombatFood, _mailStashCodecs, Monitor, () => _spaceCore, RefreshOffers, () => _ctx);

        _boardRenderer = new BoardWorldRenderer(helper, Monitor, _boards);
        _boardRenderer.Register();

        _poster = new QuestPoster(helper, Monitor, _api);
        _poster.Register();
        _poster.WireMailStashCodecs(_mailStashCodecs);

        _specialOrderWriter = new SpecialOrderWriter(helper, Monitor);
        _specialOrderWriter.Register();
        _poster.WireSpecialOrders(_specialOrderWriter);

        _shopDiscountWriter = new ShopDiscountWriter(helper, Monitor);
        _shopDiscountWriter.Register();

        _animalPurchaseDiscountWriter = new AnimalPurchaseDiscountWriter(helper, Monitor);
        _animalPurchaseDiscountWriter.Register();

        _festivalBiasWriter = new FestivalBiasWriter(Monitor);
        _fairStarTokensWriter = new FairStarTokensWriter(Monitor);

        _mailQuests = new MailQuestRegistry();

        var harmony = new Harmony(ModManifest.UniqueID);
        BillboardPatches.Apply(harmony);
        BoardCollisionPatches.Apply(harmony, _boards, helper.ModRegistry);
        MailQuestPatches.Apply(harmony, _mailQuests, _api, Monitor);
        AdventureQuestPatches.Apply(harmony);
        PlantTreesPatches.Apply(harmony, helper.ModRegistry);
        CropHarvestPatches.Apply(harmony, Monitor);
        CropHarvestPatches.CropHarvested += info => _api?.FireCropHarvested(info);
        SpecialOrdersBoardPatches.Apply(harmony, Monitor, _specialOrderWriter);
        ConsequenceDialoguePatches.Apply(harmony, Monitor);
        FestivalBiasPatches.Apply(harmony, Monitor);
        DecorShippingPatches.Apply(harmony, Monitor);
        WinterStarGiftPatch.Apply(harmony);
        DropItemsPatches.Subscribe(helper);
        MoreQuestsFramework.Rendering.DropZoneOverlay.Register(helper);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondTick;
        helper.Events.Player.Warped += OnPlayerWarped;
        helper.Events.Player.InventoryChanged += OnInventoryChanged;
        helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.Display.MenuChanged += OnMenuChanged;

        helper.ConsoleCommands.Add(
            "mq_refresh",
            "Re-rolls today's daily-board postings without reloading the save.",
            (_, _) => RefreshOffers());

        helper.ConsoleCommands.Add(
            "mq_reemit_specialorders",
            "Force-fires every SpecialOrder-source definition as if today were its StartDate "
            + "(ignores cooldown, bypasses date-match). Drops any persisted emitted entries "
            + "for those defs first so re-emission lands on a clean slate. Open the SpecialOrders "
            + "board after running to see the entries (requires SpecialOrdersBoardPages >= 2 "
            + "if vanilla's two slots are already filled by other mods' picks).",
            (_, _) => ReemitSpecialOrders());

        helper.ConsoleCommands.Add(
            "mq_trigger",
            "Force-posts a quest by definition id, bypassing IsAvailable conditions and "
            + "the trigger gate (cooldown, OneShot flag, BuildingBuilt diff, mail prereqs, etc). "
            + "Mail-kind quests are re-queued into mailForTomorrow so the letter arrives next "
            + "morning after sleeping. Usage: mq_trigger <DefinitionId>. Example: "
            + "mq_trigger Animal.MarnieCowOffer.",
            (_, args) => TriggerByDefinitionId(args));

        helper.ConsoleCommands.Add(
            "mq_givers",
            "Lists the NPCs currently eligible to be the giver for a quest, if its definition "
            + "exposes that (implements IEligibleGiverSource). Reads live game state. Usage: "
            + "mq_givers <DefinitionId> to print one quest, or mq_givers all to write a "
            + "givers-report.json for every registered quest into the More Quests Framework "
            + "mod folder. Examples: mq_givers Social.Redecorate, mq_givers all.",
            (_, args) => ListGiversByDefinitionId(args));

        helper.ConsoleCommands.Add(
            "mq_boardcount",
            "Prints how many daily quests are still unaccepted on the vanilla quest board "
            + "(the help-wanted billboard by Pierre's), then lists each one. Mirrors the "
            + "CountUnacceptedDailyBoardQuests / GetDailyBoardSlots API. Usage: mq_boardcount.",
            (_, _) => PrintBoardCount());
        // Defer GMCM + content-pack loading + RegistrationClosed until after every consumer
        // mod's GameLaunched has run.
        helper.Events.GameLoop.UpdateTicking += OnFirstTick;
    }

    public override object? GetApi() => _api;

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

    private ISpaceCoreApi? _spaceCore;

    // Without this, another mod invalidating Data/Crops or Data/CookingRecipes mid-day
    // leaves the framework's snapshot stale until the next DayStarted refresh.
    private bool _pendingAssetReload;

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        bool questsOrBoardsTouched = false;
        bool categoriesTouched = false;
        foreach (var name in e.NamesWithoutLocale)
        {
            _dataCache?.Invalidate(name.Name);
            if (name.IsEquivalentTo(QuestsAssetName) || name.IsEquivalentTo(BoardsAssetName) || name.IsEquivalentTo(NoticesAssetName))
                questsOrBoardsTouched = true;
            if (name.IsEquivalentTo(CategoriesAssetName))
                categoriesTouched = true;
        }

        // Category colors and skills can refresh live, no quest-pool reload needed.
        if (categoriesTouched)
            RebuildCategories();
        // CooldownTiers invalidation needs no action: the per-quest cooldown lookup
        // re-reads the asset on every CooldownDays access, so the next trigger pass
        // automatically picks up new tier values.
        if (!questsOrBoardsTouched)
            return;

        if (Context.IsWorldReady)
        {
            _pendingAssetReload = true;
            Monitor.Log("CP invalidated an MQF asset mid-save. Quest pool will refresh after returning to title.", LogLevel.Info);
            return;
        }

        ReloadFromAssets();
    }

    private void LoadAssetsAndRegister()
    {
        // Live lookup so GMCM edits to tier days (which invalidate the CooldownTiers asset)
        // take effect on the next CooldownDays read, no save reload needed.
        Func<string, int?> cooldownTierLookup = name =>
        {
            var dict = Helper.GameContent.Load<Dictionary<string, int>>(CooldownTiersAssetName);
            return dict.TryGetValue(name, out var days) ? days : null;
        };
        _cooldownTierLookup = cooldownTierLookup;

        RebuildCategories();

        var questsAsset = Helper.GameContent.Load<Dictionary<string, QuestDef>>(QuestsAssetName);
        _loader.LoadFromAsset(questsAsset, cooldownTierLookup);

        var boardsAsset = Helper.GameContent.Load<Dictionary<string, BoardDefinition>>(BoardsAssetName);
        _boardLoader.LoadFromAsset(boardsAsset);

        var noticesAsset = Helper.GameContent.Load<Dictionary<string, NoticeDef>>(NoticesAssetName);
        _noticeLoader.LoadFromAsset(noticesAsset);
    }

    private void RebuildCategories()
    {
        var asset = Helper.GameContent.Load<Dictionary<string, CategoryDefinition>>(CategoriesAssetName);
        Categories.Rebuild(asset);
    }

    private void ReloadFromAssets()
    {
        _registry.Clear();
        _boards.Clear();
        _notices.Clear();
        LoadAssetsAndRegister();
        _registry.Freeze();
        _boards.Freeze();
        _notices.Freeze();
        CustomBoardRouting.ValidateRouting(_registry, _boards, Monitor);
        Monitor.Log("Reloaded quests, boards, and notices from MQF assets.", LogLevel.Info);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(PadAssetRoot))
        {
            e.LoadFromModFile<Texture2D>("assets/pad.png", AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(PinAssetRoot))
        {
            e.LoadFromModFile<Texture2D>("assets/pin.png", AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(QuestsAssetName))
        {
            e.LoadFrom(() => new Dictionary<string, QuestDef>(StringComparer.OrdinalIgnoreCase), AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(BoardsAssetName))
        {
            e.LoadFrom(() => new Dictionary<string, BoardDefinition>(StringComparer.OrdinalIgnoreCase), AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(NoticesAssetName))
        {
            e.LoadFrom(() => new Dictionary<string, NoticeDef>(StringComparer.OrdinalIgnoreCase), AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(CooldownTiersAssetName))
        {
            // Seed the built-in tiers from config. Stays editable so CP packs can add their
            // own tier names on top. Reads the static Config so a GMCM reset picks up the new
            // instance on the next invalidate.
            e.LoadFrom(() => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Short"] = Config.CooldownShortDays,
                ["Medium"] = Config.CooldownMediumDays,
                ["Long"] = Config.CooldownLongDays,
            }, AssetLoadPriority.Low);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(CategoriesAssetName))
        {
            e.LoadFrom(() =>
            {
                var seed = CategoryRegistry.BuildBuiltinSeed();
                foreach (var pair in RegisteredCategories)
                    seed[pair.Key] = pair.Value;
                return seed;
            }, AssetLoadPriority.Low);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _registry.Register(new VanillaItemDelivery());
        _registry.Register(new VanillaResourceCollection());
        _registry.Register(new VanillaSlayMonster());
        _registry.Register(new VanillaFishing());

        NpcDispatch.SeedBuiltins(Dispatch);

        _spaceCore = Helper.ModRegistry.GetApi<ISpaceCoreApi>(ModCompat.SpaceCore);
        ModCompat.SpaceCoreApi = _spaceCore;
        if (_spaceCore != null)
        {
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsItemDeliveryQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsFishingQuest));
            _spaceCore.RegisterSerializerType(typeof(AdventureQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsShipQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsEarnMoneyQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsSellQuest));
            ModEntry.LogDebug("Registered framework Quest subclasses with SpaceCore.");
        }
        else
        {
            Monitor.Log(
                "SpaceCore not detected; framework Quest subclasses (item delivery, fishing, adventure) will not save. " +
                "Install SpaceCore for full functionality.",
                LogLevel.Warn);
        }

    }

    private void OnFirstTick(object? sender, UpdateTickingEventArgs e)
    {
        Helper.Events.GameLoop.UpdateTicking -= OnFirstTick;

        // Deferred one tick past GameLaunched so consumer-mod subscribers registered
        // during their own GameLaunched (after ours) actually receive these events.
        _api.FireRegistrationOpen();
        LoadAssetsAndRegister();
        _api.FireRegistrationClosed();
        _registry.Freeze();
        _boards.Freeze();
        _notices.Freeze();
        CustomBoardRouting.ValidateRouting(_registry, _boards, Monitor);

        // Reset mutates the existing Config in place instead of swapping the static. QuestContext
        // and ConsequenceEngine capture this same object at save load, so replacing it would leave
        // them on the old values until the next save load.
        GmcmRegistration.Register(Helper, ModManifest, Config, _registry, _boards, onReset: () => Config.ResetToDefaults());
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _dataCache = new GameDataCache(Monitor);
        _dataCache.Refresh();

        CombatFood.RunDataScan(Helper.GameContent);

        var items = new ItemResolver(Monitor, _dataCache);
        var ctx = new QuestContext(Helper, Monitor, Config, items, _dataCache, Dispatch);
        _ctx = ctx;
        _antiRepetition = new AntiRepetition();
        ctx.AttachAntiRepetition(_antiRepetition);

        _stateStore = new StateStore(Helper.Data, Monitor);
        _stateStore.Load();
        int prunedDeadIds = _stateStore.State.PruneDeadDefIds(_registry.RegisteredIds());
        if (prunedDeadIds > 0)
            ModEntry.LogDebug($"Pruned {prunedDeadIds} stale save-state entr{(prunedDeadIds == 1 ? "y" : "ies")} for quests no longer registered.");
        int prunedDeadNotices = _stateStore.State.PruneDeadNoticeIds(_notices.RegisteredIds());
        if (prunedDeadNotices > 0)
            ModEntry.LogDebug($"Pruned {prunedDeadNotices} stale notice save-state entr{(prunedDeadNotices == 1 ? "y" : "ies")} for notices no longer registered.");
        _antiRepetition.WireState(_stateStore.State);
        _noticeStore = new NoticeStore();
        _noticeStore.WireState(_stateStore.State);

        _triggers = new TriggerEvaluator(_stateStore.State, Monitor, _customTriggers);
        _pipeline = new QuestPipeline(ctx, _registry, _antiRepetition, _triggers);
        _pipeline.WireSkipCallback(_api.FireQuestSkippedToday);
        _api.WireState(_stateStore.State);

        _poster!.WireMailDelivery(_mailQuests, _stateStore.State);
        _specialOrderWriter?.WireState(_stateStore.State);
        _shopDiscountWriter?.WireState(_stateStore.State);
        _animalPurchaseDiscountWriter?.WireState(_stateStore.State);
        _festivalBiasWriter?.WireState(_stateStore.State);
        _fairStarTokensWriter?.WireState(_stateStore.State);
        // Persisted discounts would sit dormant until something else triggers a read.
        if (_stateStore.State.ActiveShopDiscounts.Count > 0)
            Helper.GameContent.InvalidateCache("Data/Shops");
        if (_stateStore.State.EmittedSpecialOrders.Count > 0)
            Helper.GameContent.InvalidateCache("Data/SpecialOrders");

        _mailQuests.Clear();
        var mailbox = Game1.player?.mailbox;
        var mailReceived = Game1.player?.mailReceived;
        var stillPending = new List<StashedMailQuest>();
        foreach (var stash in _stateStore.State.PendingMailDeliveries)
        {
            bool inMailbox = mailbox != null && mailbox.Contains(stash.MailKey);
            bool alreadyRead = mailReceived != null && mailReceived.Contains(stash.MailKey);
            if (alreadyRead || !inMailbox)
                continue;
            var quest = _poster.RehydrateStash(stash);
            if (quest == null)
                continue;
            _mailQuests.Register(stash.MailKey, quest, stash.OwnerUniqueId, stash.DefinitionId);
            stillPending.Add(stash);
        }
        _stateStore.State.PendingMailDeliveries = stillPending;
        if (stillPending.Count > 0)
        {
            Helper.GameContent.InvalidateCache("Data/mail");
            ModEntry.LogDebug($"Rehydrated {stillPending.Count} unread mail-quest letter(s) from save state.");
        }

        _dialogueWatcher = new DialogueWatcher(
            _registry, ctx, _stateStore.State, _api, Monitor,
            posting => _poster!.PrepareQuest(posting, daysLeft: Math.Max(1, posting.DeadlineDays)),
            _antiRepetition!);
        _dialogueWatcher.Reset();
        _poster!.WireDialogueWatcher(_dialogueWatcher);

        // Engine exposed as a static so quest-subclass questComplete overrides can fire
        // it without threading an instance reference through every subclass.
        _consequenceEngine = new ConsequenceEngine(Config, _dataCache, _stateStore.State, Monitor);
        MoreQuestsFramework.Api.ConsequenceOverrides.ApplyTo(_consequenceEngine);
        ConsequenceEngine.Active = _consequenceEngine;
        _consequenceWatcher = new ConsequenceDialogueWatcher(_stateStore.State, Monitor);
        _consequenceWatcher.Reset();
        _reportBackWatcher = new ReportBackWatcher(_reportBack, _api.ResolveOwner, Monitor);
        _reportBackWatcher.Reset();
        Patches.ConsequenceDialoguePatches.ActiveState = _stateStore.State;

        _watching.Clear();
        _seenInLog.Clear();
        _completedFired.Clear();
        Patches.DecorShippingPatches.ActiveCount = 0;

        DedupeQuestSerializedRewards();
        BackfillCancellable();
    }

    // Quests accepted before the CanBeCancelled field existed kept vanilla's non-cancellable
    // default, so the mail- and dialogue-delivered ones had no cancel button in the journal.
    // On load, bring any managed quest that predates the field (no marker) up to the new
    // cancellable default, once, and stamp the marker. A quest posted after the field exists
    // already carries the marker, so an author's deliberate opt-out (marker "false") is left
    // untouched here.
    private void BackfillCancellable()
    {
        if (Game1.player == null) return;
        var log = Game1.player.questLog;
        int fixedCount = 0;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null) continue;
            if (q.modData != null && q.modData.ContainsKey(MoreQuestsApi.ModDataCancellableKey))
                continue;
            if (!_api.TryGetManaged(q, out _))
                continue;
            q.canBeCancelled.Value = true;
            if (q.modData != null)
                q.modData[MoreQuestsApi.ModDataCancellableKey] = "true";
            fixedCount++;
        }
        if (fixedCount > 0)
            ModEntry.LogDebug($"Made {fixedCount} existing quest(s) cancellable (backfill).");
    }

    // Saves written before the SerializedRewards XmlIgnore fix (commit added 2026-05-24)
    // came back from XmlSerializer with the field's NetStringList doubled, because the
    // PascalCase property aliased the camelCase field and both members were emitted
    // and round-tripped. Pre-fix saves have already-doubled (and on each save+load
    // cycle, recursively more-doubled) lists. Dedupe in place on load so existing
    // saves heal themselves; the fix prevents future doubling.
    private void DedupeQuestSerializedRewards()
    {
        if (Game1.player == null) return;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not Rewards.IRewardedQuest rq) continue;
            var list = rq.SerializedRewards;
            if (list.Count < 2) continue;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var keep = new List<string>(list.Count);
            foreach (var line in list)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (seen.Add(line))
                    keep.Add(line);
            }
            if (keep.Count == list.Count) continue;
            int removed = list.Count - keep.Count;
            list.Clear();
            foreach (var line in keep) list.Add(line);
            Monitor.Log(
                $"Healed quest '{log[i].questTitle}': removed {removed} duplicate serializedRewards entr{(removed == 1 ? "y" : "ies")}.",
                LogLevel.Info);
        }
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        ObserveShippingBin();
        _stateStore?.Save();
    }

    // Observes only; items still get sold to the player at full price.
    private void ObserveShippingBin()
    {
        if (!Context.IsWorldReady || Game1.player == null)
            return;

        var farm = Game1.getFarm();
        if (farm == null)
            return;

        IList<Item>? bin = null;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value)
                continue;
            bool isShip = q is MoreQuestsShipQuest;
            bool isAdventure = q is AdventureQuest;
            if (!isShip && !isAdventure)
                continue;

            bin ??= farm.getShippingBin(Game1.player);
            if (bin == null || bin.Count == 0)
                return;

            if (q is MoreQuestsShipQuest s)
                s.ObserveShippingBin(bin, Monitor);
            else if (q is AdventureQuest a)
                a.ObserveShippingBin(bin);
        }
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        _stateStore?.Save();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        // Fire before tearing down save-bound state so consumer-mod handlers can still
        // read whatever they need before everything clears.
        _api.FireFrameworkShuttingDown();
        _api.ClearState();

        // Per-save state hygiene. Without these, loading a second save in the same
        // session leaves the first save's mail registry entries and reward writers
        // alive, so MailQuestPatches can hand back a quest from the wrong save and
        // a granted reward routes into state that no longer belongs to the loaded
        // farm.
        _mailQuests.Clear();
        _shopDiscountWriter?.ClearActive();
        _animalPurchaseDiscountWriter?.ClearActive();
        FestivalBiasWriter.ClearActive();
        FairStarTokensWriter.ClearActive();

        if (_pendingAssetReload)
        {
            _pendingAssetReload = false;
            ReloadFromAssets();
        }
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady || _pipeline == null || _poster == null)
            return;
        // Host-only stance. Save data is host-scoped (see StateStore), so letting
        // farmhands run any of this would mutate per-session state on their side
        // that the host never agrees with.
        if (!Game1.IsMasterGame)
            return;

        _dataCache?.Refresh();
        _antiRepetition?.BeginDay();
        _triggers?.BeginDay();
        _poster.BeginDay();
        _dialogueWatcher?.ResetDay();

        ObserveBuildOnQuestLog();

        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);
        _poster.CommitBoard();

        var triggered = _pipeline.GenerateTriggered();
        _poster.PostBatch(triggered);

        // Sweep before emit so a yearly re-fire doesn't collide with a stale entry.
        _specialOrderWriter?.SweepExpired();
        var specialOrders = _pipeline.GenerateSpecialOrders();
        _poster.PostBatch(specialOrders);

        if (_dialogueWatcher != null)
        {
            _dialogueWatcher.PruneUnavailable();
            foreach (var (def, npc) in _pipeline.GenerateNpcDialogueQueue())
                _dialogueWatcher.Enqueue(def.Id, npc);
        }

        CustomBoardSlots.ClearAll();
        var customDraws = _pipeline.GenerateCustomBoardPostings(_boards);
        var slotsByBoardKey = new Dictionary<string, List<CustomBoardSlots.Slot>>(StringComparer.OrdinalIgnoreCase);
        foreach (var draw in customDraws)
        {
            var quest = _poster.PrepareCustomBoardQuest(draw.Posting);
            if (quest == null)
                continue;
            string homeKey = draw.Boards.Count > 0
                ? (draw.Boards[0].OwnerUniqueId ?? "") + "/" + (draw.Boards[0].Name ?? "")
                : "";
            var slot = new CustomBoardSlots.Slot(quest, draw.Posting, homeKey);
            foreach (var board in draw.Boards)
            {
                string key = (board.OwnerUniqueId ?? "") + "/" + (board.Name ?? "");
                if (!slotsByBoardKey.TryGetValue(key, out var list))
                    slotsByBoardKey[key] = list = new List<CustomBoardSlots.Slot>();
                list.Add(slot);
            }
        }

        // Notice (bulletin) pins draw into their own per-board budget and append after the
        // quest pins, so a notice-free board's slot list is identical to before.
        if (_noticeStore != null)
        {
            foreach (var noticeDraw in _pipeline.GenerateBoardNotices(_notices, _boards, _noticeStore, _cooldownTierLookup))
            {
                string key = (noticeDraw.Board.OwnerUniqueId ?? "") + "/" + (noticeDraw.Board.Name ?? "");
                var slot = new CustomBoardSlots.Slot(noticeDraw.Notice, key);
                if (!slotsByBoardKey.TryGetValue(key, out var list))
                    slotsByBoardKey[key] = list = new List<CustomBoardSlots.Slot>();
                list.Add(slot);
            }
        }

        foreach (var (key, slots) in slotsByBoardKey)
            CustomBoardSlots.SetSlotsByKey(key, slots);

        // Single source of truth on the board.
        Game1.netWorldState.Value.SetQuestOfTheDay(null);

        // Catches quests accepted on a previous session where the player already
        // descended past the target floor.
        ObserveReachLevelOnQuestLog();

        SweepShopDiscounts();
        _animalPurchaseDiscountWriter?.SweepExpired();

        _festivalBiasWriter?.SweepExpired();
        _fairStarTokensWriter?.SweepExpired();
        _fairTokensAppliedThisSession = false;

        _consequenceEngine?.SweepExpired();

        _api.FireDayRefreshed(daily.Count, triggered.Count);
    }

    private void OnPlayerWarped(object? sender, StardewModdingAPI.Events.WarpedEventArgs e)
    {
        if (!Context.IsWorldReady || !e.IsLocalPlayer)
            return;
        ObserveReachLevelOnQuestLog();
        ObserveVisitOnQuestLog(e.NewLocation?.Name);
    }

    private void ObserveVisitOnQuestLog(string? locationName)
    {
        if (string.IsNullOrEmpty(locationName) || Game1.player == null)
            return;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is AdventureQuest a && !a.completed.Value)
                a.ObserveVisit(locationName);
        }
    }

    private void OnTerrainFeatureListChanged(object? sender, StardewModdingAPI.Events.TerrainFeatureListChangedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null || e.Location == null)
            return;
        int trees = 0;
        foreach (var pair in e.Added)
        {
            if (pair.Value is StardewValley.TerrainFeatures.Tree)
                trees++;
        }
        if (trees == 0)
            return;
        string locName = e.Location.Name;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is AdventureQuest a && !a.completed.Value)
                a.ObservePlantedTree(locName, trees);
        }
    }

    // Snapshot of museumPieces.Length captured when MuseumMenu opens. On menu close,
    // a positive delta means the player donated that many pieces. Rearranging is a
    // remove-then-re-add inside the same menu session, so it nets to zero.
    private int? _museumPieceCountOnMenuOpen;

    private void OnMenuChanged(object? sender, StardewModdingAPI.Events.MenuChangedEventArgs e)
    {
        if (e.NewMenu is StardewValley.Menus.MuseumMenu)
        {
            _museumPieceCountOnMenuOpen = Game1.netWorldState?.Value?.MuseumPieces?.Length ?? 0;
            return;
        }

        if (e.OldMenu is StardewValley.Menus.MuseumMenu && _museumPieceCountOnMenuOpen.HasValue)
        {
            int before = _museumPieceCountOnMenuOpen.Value;
            _museumPieceCountOnMenuOpen = null;
            int after = Game1.netWorldState?.Value?.MuseumPieces?.Length ?? 0;
            int delta = after - before;
            if (delta <= 0 || Game1.player?.questLog == null)
                return;
            var log = Game1.player.questLog;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i] is AdventureQuest a && !a.completed.Value)
                    a.ObserveMuseumDonation(delta);
            }
        }
    }

    private void OnObjectListChanged(object? sender, StardewModdingAPI.Events.ObjectListChangedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null || e.Location == null)
            return;
        // The day-rollover refresh removes yesterday's litter from every outdoor map,
        // which would otherwise close the step in one shot. Gate by player presence.
        if (Game1.currentLocation == null || !ReferenceEquals(e.Location, Game1.currentLocation))
            return;
        bool outdoors = e.Location.IsOutdoors;
        int weeds = 0;
        int debris = 0;
        int artifactSpots = 0;
        foreach (var pair in e.Removed)
        {
            var obj = pair.Value;
            if (obj == null) continue;
            if (obj.IsWeeds())
                weeds++;
            else if (obj.IsTwig())
                debris++;
            else if (outdoors && obj.IsBreakableStone())
                debris++;
            else if (obj.QualifiedItemId == "(O)590")
                artifactSpots++;
        }
        if (weeds == 0 && debris == 0 && artifactSpots == 0)
            return;
        string locName = e.Location.Name;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not AdventureQuest a || a.completed.Value) continue;
            if (weeds > 0) a.ObserveWeedsCleared(locName, weeds);
            if (debris > 0) a.ObserveDebrisCleared(locName, debris);
            if (artifactSpots > 0) a.ObserveArtifactSpotDug(locName, artifactSpots);
        }
    }

    private void ObserveReachLevelOnQuestLog()
    {
        if (Game1.player == null)
            return;
        int deepest = Game1.player.deepestMineLevel;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is AdventureQuest a && !a.completed.Value)
                a.ObserveReachLevel(deepest);
        }
    }

    private void ObserveEarnMoneyOnQuestLog()
    {
        if (Game1.player == null)
            return;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is MoreQuestsEarnMoneyQuest m && !m.completed.Value)
                m.ObserveMoney();
        }
    }

    // Counts items the player sells across a shop counter toward any Sell quest. Selling
    // drops the stack out of the player's inventory while the shop menu is open, so a drop
    // here is a sale. Buying adds or grows a stack, which we skip.
    private void OnInventoryChanged(object? sender, StardewModdingAPI.Events.InventoryChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !e.IsLocalPlayer || Game1.player == null)
            return;
        if (Game1.activeClickableMenu is not StardewValley.Menus.ShopMenu shop)
            return;
        string shopId = shop.ShopId ?? string.Empty;
        if (string.IsNullOrEmpty(shopId))
            return;

        bool anySellQuest = false;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is MoreQuestsSellQuest sq && !sq.completed.Value)
            {
                anySellQuest = true;
                break;
            }
        }
        if (!anySellQuest)
            return;

        var sells = new List<(Item item, int count)>();
        foreach (var item in e.Removed)
        {
            if (item != null && item.Stack > 0)
                sells.Add((item, item.Stack));
        }
        foreach (var change in e.QuantityChanged)
        {
            int dropped = change.OldSize - change.NewSize;
            if (change.Item != null && dropped > 0)
                sells.Add((change.Item, dropped));
        }
        if (sells.Count == 0)
            return;

        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not MoreQuestsSellQuest sq || sq.completed.Value)
                continue;
            foreach (var (item, count) in sells)
                sq.ObserveSale(shopId, item, count);
        }
    }

    private void ObserveBuildOnQuestLog()
    {
        if (Game1.player == null || _triggers == null)
            return;
        var newTypes = _triggers.NewBuildingsToday;
        if (newTypes == null || newTypes.Count == 0)
            return;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is AdventureQuest a && !a.completed.Value)
                a.ObserveBuild(newTypes);
        }
    }

    // Keeps the gated canBeShipped postfix on a fast path when no decor-shipping quest
    // is in the log, and hands the postfix one predicate per active quest so only the
    // item ids that quest actually wants get the shipping override.
    private static void RecomputeDecorShippingCount()
    {
        if (Game1.player == null)
        {
            Patches.DecorShippingPatches.ActiveCount = 0;
            Patches.DecorShippingPatches.SetPredicates(System.Array.Empty<System.Func<StardewValley.Object, bool>>());
            return;
        }
        int count = 0;
        var predicates = new List<System.Func<StardewValley.Object, bool>>();
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value)
                continue;
            if (q is Quests.AdventureQuest a && a.HasDecorShippingStep)
            {
                count++;
                predicates.Add(a.CanShipForDecor);
            }
            else if (q is Quests.MoreQuestsShipQuest s && s.allowDecorShipping.Value)
            {
                count++;
                predicates.Add(s.MatchesItem);
            }
        }
        Patches.DecorShippingPatches.ActiveCount = count;
        Patches.DecorShippingPatches.SetPredicates(predicates);
    }

    // Idempotent across the festival session; writer's Consume also drops pending
    // entries so a re-entry same day can't double-grant.
    private void ApplyFairStarTokensIfFairActive()
    {
        if (_fairTokensAppliedThisSession)
            return;
        if (_fairStarTokensWriter == null || Game1.player == null)
            return;
        if (!Game1.isFestival())
            return;
        // Matches on the festival's name rather than the vanilla fall-16 slot so a
        // content mod that moves the Fair to a different day still credits tokens.
        if (!string.Equals(Game1.CurrentEvent?.FestivalName, "Stardew Valley Fair", StringComparison.Ordinal))
            return;
        int amount = _fairStarTokensWriter.PeekAmount();
        if (amount <= 0)
            return;
        Game1.player.festivalScore += amount;
        _fairStarTokensWriter.Consume();
        _fairTokensAppliedThisSession = true;
        ModEntry.LogDebug($"FairStarTokens applied: +{amount} festivalScore.");
    }

    private void PollClumpsOnQuestLog()
    {
        if (Game1.player == null)
            return;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not AdventureQuest a || a.completed.Value)
                continue;
            // Polling at currentLocation handles the common case (player breaks a clump
            // where they stand). Multi-location quests re-baseline on the next warp.
            a.PollResourceClumps(Game1.currentLocation);
            a.ObserveDecorate(Game1.currentLocation);
            a.ObserveCraft();
            a.PollCustomSteps(step => ResolveCustomStepHandler(a, step));
        }
    }

    private Func<CustomStepContext, int>? ResolveCustomStepHandler(AdventureQuest quest, AdventureStepState step)
    {
        if (step.Targets.Count == 0)
            return null;
        string handlerName = step.Targets[0];
        if (string.IsNullOrEmpty(handlerName))
            return null;
        string owner = _api.TryGetManaged(quest, out var info) ? info.OwnerUniqueId : ModManifest.UniqueID;
        return _customSteps.Resolve(owner, handlerName);
    }

    private void SweepShopDiscounts()
    {
        if (_stateStore == null) return;
        var state = _stateStore.State;
        if (state.ActiveShopDiscounts.Count == 0) return;

        int today = Game1.Date.TotalDays;
        var dropped = new HashSet<string>();
        for (int i = state.ActiveShopDiscounts.Count - 1; i >= 0; i--)
        {
            var d = state.ActiveShopDiscounts[i];
            if (today > d.ExpiresAfterDay)
            {
                dropped.Add(d.ShopId);
                state.ActiveShopDiscounts.RemoveAt(i);
            }
        }
        if (dropped.Count > 0)
            Helper.GameContent.InvalidateCache("Data/Shops");
    }

    // Test/debug helper. Force-fires every SpecialOrder-source definition regardless of
    // date or cooldown, dropping any persisted emit records for those defs first.
    private void ReemitSpecialOrders()
    {
        if (!Context.IsWorldReady || _pipeline == null || _poster == null || _stateStore == null)
        {
            Monitor.Log("mq_reemit_specialorders ignored: world not ready or pipeline not initialised.", LogLevel.Warn);
            return;
        }

        var ctx = new QuestContext(Helper, Monitor, Config, new ItemResolver(Monitor, _dataCache!), _dataCache!, Dispatch);
        var state = _stateStore.State;
        int emitted = 0;
        int skipped = 0;

        foreach (var def in _registry.All)
        {
            if (def.Source != TriggerSource.SpecialOrder)
                continue;
            state.EmittedSpecialOrders.RemoveAll(e => e.DefinitionId == def.Id);
            state.LastFiredDay.Remove(def.Id);

            if (!def.IsAvailable(ctx))
            {
                skipped++;
                Monitor.Log($"mq_reemit_specialorders: '{def.Id}' skipped (Available conditions not met).", LogLevel.Info);
                continue;
            }

            var posting = def.Build(ctx);
            if (posting?.SpecialOrder == null)
            {
                skipped++;
                Monitor.Log($"mq_reemit_specialorders: '{def.Id}' skipped (generator returned null or no SpecialOrder spec).", LogLevel.Info);
                continue;
            }
            if (string.IsNullOrEmpty(posting.OwnerUniqueId))
                posting.OwnerUniqueId = def.OwnerUniqueId;
            posting.Kind = PostingKind.SpecialOrder;
            _poster.Post(posting);
            emitted++;
        }

        Monitor.Log($"mq_reemit_specialorders: emitted {emitted}, skipped {skipped}. Open the SpecialOrders board to view.", LogLevel.Info);
    }

    private void TriggerByDefinitionId(string[] args)
    {
        if (args.Length < 1)
        {
            Monitor.Log("Usage: mq_trigger <DefinitionId>. Tip: mq_refresh re-rolls daily-board picks; this command targets the triggered path (mail, OneShot, BuildingBuilt, etc).", LogLevel.Info);
            return;
        }
        if (!Context.IsWorldReady || _poster == null || _ctx == null)
        {
            Monitor.Log("mq_trigger ignored: load a save first.", LogLevel.Warn);
            return;
        }

        string id = args[0];
        if (!_registry.TryGet(id, out var def) || def == null)
        {
            Monitor.Log($"mq_trigger: no quest definition registered with id '{id}'. Run with a valid id from `RegisteredQuestIds()` or your content pack.", LogLevel.Warn);
            return;
        }

        var posting = def.Build(_ctx);
        if (posting == null)
        {
            Monitor.Log($"mq_trigger: '{id}'.Build returned null. The generator may need specific live conditions (e.g. an NPC present, a pool of items, etc) that pre-req bypass can't fix.", LogLevel.Warn);
            return;
        }
        if (string.IsNullOrEmpty(posting.OwnerUniqueId))
            posting.OwnerUniqueId = def.OwnerUniqueId;
        posting.Kind = def.Kind;

        // Daily-board quests go straight onto the live board. PostBatch only buffers them
        // for the next CommitBoard, so without this the quest would never show.
        if (posting.Kind == PostingKind.DailyBoard)
        {
            if (_poster.PostToBoardImmediate(posting) == null)
                return;
            Monitor.Log($"mq_trigger: posted '{id}' to the billboard. Open the board now to see it. Heads up: mq_refresh re-rolls the board and will drop it if it's on cooldown or out of its day range.", LogLevel.Info);
            return;
        }

        var preMailbox = new HashSet<string>(Game1.player.mailbox);
        _poster.PostBatch(new[] { posting });

        if (posting.Kind == PostingKind.Mail)
        {
            string? newKey = null;
            foreach (string key in Game1.player.mailbox)
            {
                if (!preMailbox.Contains(key)) { newKey = key; break; }
            }
            if (newKey != null)
            {
                Game1.player.mailbox.Remove(newKey);
                if (!Game1.player.mailForTomorrow.Contains(newKey))
                    Game1.player.mailForTomorrow.Add(newKey);
                Monitor.Log($"mq_trigger: posted '{id}' as mail. Letter queued for tomorrow's mailbox (key '{newKey}').", LogLevel.Info);
                return;
            }
        }

        Monitor.Log($"mq_trigger: posted '{id}' ({posting.Kind}).", LogLevel.Info);
    }

    private void PrintBoardCount()
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Log("mq_boardcount ignored: load a save first.", LogLevel.Warn);
            return;
        }

        var slots = _api.GetDailyBoardSlots();
        Monitor.Log($"{_api.CountUnacceptedDailyBoardQuests()} unaccepted quest(s) on the vanilla board.", LogLevel.Info);
        foreach (var slot in slots)
            Monitor.Log($"  {slot.DefinitionId} (owner {slot.OwnerUniqueId})", LogLevel.Info);
    }

    private void ListGiversByDefinitionId(string[] args)
    {
        if (args.Length < 1)
        {
            Monitor.Log("Usage: mq_givers <DefinitionId> | all. Example: mq_givers Social.Redecorate.", LogLevel.Info);
            return;
        }
        if (!Context.IsWorldReady)
        {
            Monitor.Log("mq_givers ignored: load a save first.", LogLevel.Warn);
            return;
        }

        if (string.Equals(args[0], "all", System.StringComparison.OrdinalIgnoreCase))
        {
            WriteGiversReport();
            return;
        }

        string id = args[0];
        if (!_registry.TryGet(id, out var def) || def == null)
        {
            Monitor.Log($"mq_givers: no quest definition registered with id '{id}'. Run with a valid id from `RegisteredQuestIds()`.", LogLevel.Warn);
            return;
        }
        if (def is not IEligibleGiverSource source)
        {
            Monitor.Log($"mq_givers: '{id}' doesn't expose an eligible-giver list (its definition doesn't implement IEligibleGiverSource).", LogLevel.Info);
            return;
        }

        var givers = source.GetEligibleGivers();
        if (givers == null || givers.Count == 0)
        {
            Monitor.Log($"mq_givers: '{id}' has no eligible givers right now.", LogLevel.Info);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"mq_givers: '{id}' has {givers.Count} eligible giver(s):");
        foreach (string name in givers)
        {
            string display = NpcDisplay.Resolve(name);
            sb.Append(string.Equals(display, name, System.StringComparison.Ordinal)
                ? $"\n  - {name}"
                : $"\n  - {display} ({name})");
        }
        Monitor.Log(sb.ToString(), LogLevel.Info);
    }

    private void WriteGiversReport()
    {
        var quests = new List<GiversReportEntry>();
        int exposing = 0;

        foreach (string id in _registry.RegisteredIds())
        {
            if (!_registry.TryGet(id, out var def) || def == null)
                continue;

            var entry = new GiversReportEntry
            {
                Id = def.Id,
                Owner = def.OwnerUniqueId,
                Category = def.Category,
                Kind = def.Kind.ToString(),
                ExposesGivers = def is IEligibleGiverSource,
            };

            if (def is IEligibleGiverSource source)
            {
                exposing++;
                var rows = new List<GiverRow>();
                var givers = source.GetEligibleGivers();
                if (givers != null)
                {
                    foreach (string name in givers)
                        rows.Add(new GiverRow { Name = name, DisplayName = NpcDisplay.Resolve(name) });
                }
                entry.EligibleGiverCount = rows.Count;
                entry.EligibleGivers = rows;
            }

            quests.Add(entry);
        }

        var report = new GiversReport
        {
            Season = Game1.currentSeason,
            DayOfMonth = Game1.dayOfMonth,
            Year = Game1.year,
            TotalQuests = quests.Count,
            QuestsExposingGivers = exposing,
            Quests = quests,
        };

        string saveName = StardewModdingAPI.Constants.SaveFolderName ?? "unknown";
        string fileName = $"givers_report_{saveName}.json";
        Helper.Data.WriteJsonFile(fileName, report);
        Monitor.Log(
            $"mq_givers all: wrote {quests.Count} quest(s) ({exposing} expose a giver list) to '{fileName}' "
            + "in the More Quests Framework mod folder.",
            LogLevel.Info);
    }

    private sealed class GiversReport
    {
        public string Season { get; set; } = string.Empty;
        public int DayOfMonth { get; set; }
        public int Year { get; set; }
        public int TotalQuests { get; set; }
        public int QuestsExposingGivers { get; set; }
        public List<GiversReportEntry> Quests { get; set; } = new();
    }

    private sealed class GiversReportEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool ExposesGivers { get; set; }
        // Null when the quest doesn't expose givers; empty when it does but nobody qualifies now.
        public int? EligibleGiverCount { get; set; }
        public List<GiverRow>? EligibleGivers { get; set; }
    }

    private sealed class GiverRow
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    private void RefreshOffers()
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Log("RefreshOffers ignored: world not ready.", LogLevel.Warn);
            return;
        }
        if (_pipeline == null || _poster == null)
        {
            Monitor.Log("RefreshOffers ignored: pipeline not initialised.", LogLevel.Warn);
            return;
        }

        _dataCache?.Refresh();
        // Otherwise the re-roll pool is empty (every def is on its own freshly-recorded
        // cooldown) and tomorrow's batch ends up blocked too.
        _antiRepetition?.RewindToDayStart();
        _poster.BeginDay();
        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);
        _poster.CommitBoard();
        Monitor.Log($"RefreshOffers: re-rolled {daily.Count} daily postings.", LogLevel.Info);
        _api.FireDayRefreshed(daily.Count, 0);
    }

    private void OnOneSecondTick(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null)
            return;
        if (!Game1.IsMasterGame)
            return;

        _dialogueWatcher?.Tick();
        _consequenceWatcher?.Tick();
        _reportBackWatcher?.Tick();
        PollClumpsOnQuestLog();
        ObserveEarnMoneyOnQuestLog();
        ApplyFairStarTokensIfFairActive();
        // Bypasses Data/SpecialOrders.Rewards entirely so third-party content packs
        // that mutate that array can't intercept the grant.
        _specialOrderWriter?.CheckCompletionsAndGrantRewards();

        bool logChanged = false;
        var current = Game1.player.questLog;
        _seenThisTick.Clear();
        for (int i = 0; i < current.Count; i++)
        {
            var q = current[i];
            if (q == null)
                continue;
            if (!_api.TryGetManaged(q, out var info))
                continue;

            _seenThisTick.Add(q);
            if (!_seenInLog.ContainsKey(q))
            {
                _seenInLog[q] = q.daysLeft.Value > 0 || q.dailyQuest.Value;
                _api.FireQuestAccepted(q, info);
                logChanged = true;
                // Letter has been opened (vanilla addQuest pushed quest into the log).
                if (!string.IsNullOrEmpty(q.id.Value))
                    _poster?.DropStash(q.id.Value);
            }

            if (q.completed.Value && _completedFired.Add(q))
            {
                _api.FireQuestCompleted(q, info);
                logChanged = true;
            }
        }

        if (_seenInLog.Count > 0)
        {
            var removed = new List<Quest>();
            foreach (var pair in _seenInLog)
            {
                if (!_seenThisTick.Contains(pair.Key))
                    removed.Add(pair.Key);
            }
            for (int i = 0; i < removed.Count; i++)
            {
                var q = removed[i];
                bool wasTimed = _seenInLog[q];
                _seenInLog.Remove(q);
                bool firedCompletedThisRun = _completedFired.Remove(q);
                bool wasCompleted = firedCompletedThisRun || q.completed.Value;
                if (!_api.TryGetManaged(q, out var info))
                    continue;
                logChanged = true;
                // Vanilla Quest.questComplete yanks reward-less quests out of questLog in
                // the same call that flips completed.Value, so the in-log loop never sees
                // them as completed; fire here before the QuestRemoved that follows.
                if (wasCompleted && !firedCompletedThisRun)
                    _api.FireQuestCompleted(q, info);
                QuestRemovalReason reason;
                if (wasCompleted)
                    reason = QuestRemovalReason.Completed;
                else if (wasTimed && q.daysLeft.Value <= 0)
                    reason = QuestRemovalReason.Expired;
                else
                    reason = QuestRemovalReason.Cancelled;
                _api.FireQuestRemoved(q, info, reason);
            }
        }

        if (logChanged)
            RecomputeDecorShippingCount();
    }
}
