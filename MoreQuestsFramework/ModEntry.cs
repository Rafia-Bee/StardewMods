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
    internal static MoreQuestsFrameworkConfig Config { get; set; } = new();
    internal static ITranslationHelper? Translation { get; private set; }

    internal const string PadAssetRoot = "Mods/RafiaBee.MoreQuestsFramework/Pad";
    internal const string PinAssetRoot = "Mods/RafiaBee.MoreQuestsFramework/Pin";
    internal const string QuestsAssetName = "Mods/RafiaBee.MoreQuestsFramework/Quests";
    internal const string BoardsAssetName = "Mods/RafiaBee.MoreQuestsFramework/Boards";
    internal const string CooldownTiersAssetName = "Mods/RafiaBee.MoreQuestsFramework/CooldownTiers";

    private QuestRegistry _registry = null!;
    private GeneratorRegistry _generators = null!;
    private CustomStepRegistry _customSteps = null!;
    private CustomTriggerRegistry _customTriggers = null!;
    private CustomRewardRegistry _customRewards = null!;
    private CustomConditionRegistry _customConditions = null!;
    private CustomBoardQuestRegistry _customBoardQuests = null!;
    private QuestPackLoader _loader = null!;
    private BoardRegistry _boards = null!;
    private BoardPackLoader _boardLoader = null!;
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

        _registry = new QuestRegistry(Monitor);
        _generators = new GeneratorRegistry(Monitor);
        _customSteps = new CustomStepRegistry(Monitor);
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
        Dispatch = new DispatchRegistry(helper.ModRegistry, Monitor);
        CombatFood = new CombatFoodRegistry(Monitor);
        _mailStashCodecs = new MailStashCodecRegistry(Monitor);
        _mailStashCodecs.Register(AdventureQuestStashCodec.Kind, typeof(AdventureQuest), AdventureQuestStashCodec.Encode, AdventureQuestStashCodec.Decode);
        _mailStashCodecs.Register(MoreQuestsShipQuestStashCodec.Kind, typeof(MoreQuestsShipQuest), MoreQuestsShipQuestStashCodec.Encode, MoreQuestsShipQuestStashCodec.Decode);
        _api = new MoreQuestsApi(_registry, _generators, _customSteps, _customTriggers, _customRewards, _customConditions, _customBoardQuests, _loader, _boardLoader, Dispatch, _boards, CombatFood, _mailStashCodecs, Monitor, () => _spaceCore, RefreshOffers, () => _ctx);

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
        SpecialOrdersBoardPatches.Apply(harmony, Monitor, _specialOrderWriter);
        ConsequenceDialoguePatches.Apply(harmony, Monitor);
        FestivalBiasPatches.Apply(harmony, Monitor);
        DecorShippingPatches.Apply(harmony, Monitor);
        WinterStarGiftPatch.Apply(harmony);

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
        helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;

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
        bool oursTouched = false;
        foreach (var name in e.NamesWithoutLocale)
        {
            _dataCache?.Invalidate(name.Name);
            if (name.IsEquivalentTo(QuestsAssetName) || name.IsEquivalentTo(BoardsAssetName) || name.IsEquivalentTo(CooldownTiersAssetName))
                oursTouched = true;
        }
        if (!oursTouched)
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
        var tiersAsset = Helper.GameContent.Load<Dictionary<string, int>>(CooldownTiersAssetName);
        var questsAsset = Helper.GameContent.Load<Dictionary<string, QuestDef>>(QuestsAssetName);
        _loader.LoadFromAsset(questsAsset, tiersAsset, Helper.Translation);

        var boardsAsset = Helper.GameContent.Load<Dictionary<string, BoardDefinition>>(BoardsAssetName);
        _boardLoader.LoadFromAsset(boardsAsset);
    }

    private void ReloadFromAssets()
    {
        _registry.Clear();
        _boards.Clear();
        LoadAssetsAndRegister();
        _registry.Freeze();
        _boards.Freeze();
        Monitor.Log("Reloaded quests and boards from MQF assets.", LogLevel.Info);
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

        if (e.NameWithoutLocale.IsEquivalentTo(CooldownTiersAssetName))
        {
            e.LoadFrom(() => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), AssetLoadPriority.Low);
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
        if (_spaceCore != null)
        {
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsItemDeliveryQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsFishingQuest));
            _spaceCore.RegisterSerializerType(typeof(AdventureQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsShipQuest));
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

        GmcmRegistration.Register(Helper, ModManifest, Config, _registry, onReset: () => Config = new MoreQuestsFrameworkConfig());
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

        _stateStore = new StateStore(Helper.Data, Monitor);
        _stateStore.Load();
        int prunedDeadIds = _stateStore.State.PruneDeadDefIds(_registry.RegisteredIds());
        if (prunedDeadIds > 0)
            ModEntry.LogDebug($"Pruned {prunedDeadIds} stale save-state entr{(prunedDeadIds == 1 ? "y" : "ies")} for quests no longer registered.");
        _antiRepetition.WireState(_stateStore.State);

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
            posting => _poster!.PrepareQuest(posting, daysLeft: Math.Max(1, posting.DeadlineDays)));
        _dialogueWatcher.Reset();
        _poster!.WireDialogueWatcher(_dialogueWatcher);

        // Engine exposed as a static so quest-subclass questComplete overrides can fire
        // it without threading an instance reference through every subclass.
        _consequenceEngine = new ConsequenceEngine(Config, _dataCache, _stateStore.State, Monitor);
        MoreQuestsFramework.Api.ConsequenceOverrides.ApplyTo(_consequenceEngine);
        ConsequenceEngine.Active = _consequenceEngine;
        _consequenceWatcher = new ConsequenceDialogueWatcher(_stateStore.State, Monitor);
        _consequenceWatcher.Reset();
        Patches.ConsequenceDialoguePatches.ActiveState = _stateStore.State;

        _watching.Clear();
        _seenInLog.Clear();
        _completedFired.Clear();
        Patches.DecorShippingPatches.ActiveCount = 0;
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
            foreach (var (def, npc) in _pipeline.GenerateNpcDialogueQueue())
                _dialogueWatcher.Enqueue(def.Id, npc);
        }

        CustomBoardSlots.ClearAll();
        var customByBoard = _pipeline.GenerateCustomBoardPostings(_boards);
        foreach (var (_, perBoard) in customByBoard)
        {
            if (perBoard.Count == 0) continue;
            var board = perBoard[0].board;
            var entries = new List<(StardewValley.Quests.Quest q, QuestPosting p)>(perBoard.Count);
            foreach (var (posting, _) in perBoard)
            {
                var quest = _poster.PrepareCustomBoardQuest(posting);
                if (quest != null)
                    entries.Add((quest, posting));
            }
            CustomBoardSlots.Replace(board, entries, Monitor);
        }

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

    private void OnObjectListChanged(object? sender, StardewModdingAPI.Events.ObjectListChangedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null || e.Location == null)
            return;
        bool outdoors = e.Location.IsOutdoors;
        int weeds = 0;
        int debris = 0;
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
        }
        if (weeds == 0 && debris == 0)
            return;
        string locName = e.Location.Name;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not AdventureQuest a || a.completed.Value) continue;
            if (weeds > 0) a.ObserveWeedsCleared(locName, weeds);
            if (debris > 0) a.ObserveDebrisCleared(locName, debris);
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
        PollClumpsOnQuestLog();
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
