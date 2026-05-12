using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Patches;
using MoreQuestsFramework.Pipeline;
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

    internal const string PadAssetRoot = "Mods/RafiaBee.MoreQuestsFramework/Pad";
    internal const string PinAssetRoot = "Mods/RafiaBee.MoreQuestsFramework/Pin";

    private QuestRegistry _registry = null!;
    private GeneratorRegistry _generators = null!;
    private QuestPackLoader _loader = null!;
    private BoardRegistry _boards = null!;
    private BoardPackLoader _boardLoader = null!;
    private BoardWorldRenderer? _boardRenderer;
    private QuestPipeline? _pipeline;
    private QuestPoster? _poster;
    private GameDataCache? _dataCache;
    private AntiRepetition? _antiRepetition;
    private MoreQuestsApi _api = null!;
    private StateStore? _stateStore;
    private TriggerEvaluator? _triggers;
    private DialogueWatcher? _dialogueWatcher;
    private MailQuestRegistry _mailQuests = null!;
    private SpecialOrderWriter? _specialOrderWriter;
    private ShopDiscountWriter? _shopDiscountWriter;
    private AnimalPurchaseDiscountWriter? _animalPurchaseDiscountWriter;
    private FestivalBiasWriter? _festivalBiasWriter;
    private ConsequenceEngine? _consequenceEngine;
    private ConsequenceDialogueWatcher? _consequenceWatcher;

    internal DispatchRegistry Dispatch { get; private set; } = null!;
    internal CombatFoodRegistry CombatFood { get; private set; } = null!;
    internal MoreQuestsApi Api => _api;

    private readonly HashSet<Quest> _watching = new();
    private readonly HashSet<Quest> _seenInLog = new();
    private readonly HashSet<Quest> _completedFired = new();

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<MoreQuestsFrameworkConfig>();

        _registry = new QuestRegistry(Monitor);
        _generators = new GeneratorRegistry(Monitor);
        _loader = new QuestPackLoader(_registry, _generators, Monitor);
        _boards = new BoardRegistry(Monitor);
        _boardLoader = new BoardPackLoader(_boards, Monitor);
        Dispatch = new DispatchRegistry(helper.ModRegistry, Monitor);
        CombatFood = new CombatFoodRegistry(Monitor);
        _api = new MoreQuestsApi(_registry, _generators, _loader, _boardLoader, Dispatch, _boards, CombatFood, Monitor, () => _spaceCore, RefreshOffers);

        _boardRenderer = new BoardWorldRenderer(helper, Monitor, _boards);
        _boardRenderer.Register();

        _poster = new QuestPoster(helper, Monitor, _api);
        _poster.Register();

        _specialOrderWriter = new SpecialOrderWriter(helper, Monitor);
        _specialOrderWriter.Register();
        _poster.WireSpecialOrders(_specialOrderWriter);

        _shopDiscountWriter = new ShopDiscountWriter(helper, Monitor);
        _shopDiscountWriter.Register();

        _animalPurchaseDiscountWriter = new AnimalPurchaseDiscountWriter(helper, Monitor);
        _animalPurchaseDiscountWriter.Register();

        _festivalBiasWriter = new FestivalBiasWriter(Monitor);

        _mailQuests = new MailQuestRegistry();

        var harmony = new Harmony(ModManifest.UniqueID);
        BillboardPatches.Apply(harmony);
        BoardCollisionPatches.Apply(harmony, _boards);
        MailQuestPatches.Apply(harmony, _mailQuests, _api, Monitor);
        AdventureQuestPatches.Apply(harmony);
        PlantTreesPatches.Apply(harmony, helper.ModRegistry);
        SpecialOrdersBoardPatches.Apply(harmony, Monitor, _specialOrderWriter);
        ConsequenceDialoguePatches.Apply(harmony, Monitor);
        FestivalBiasPatches.Apply(harmony, Monitor);
        DecorShippingPatches.Apply(harmony, Monitor);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
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
        // mod's `GameLaunched` has run, so their registered quests appear in GMCM and
        // any content pack that references their generators sees them in the registry.
        helper.Events.GameLoop.UpdateTicking += OnFirstTick;
    }

    public override object? GetApi() => _api;

    private ISpaceCoreApi? _spaceCore;

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
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // Register the framework's own four vanilla wrappers before consumer mods see RegistrationOpen.
        _registry.Register(new VanillaItemDelivery());
        _registry.Register(new VanillaResourceCollection());
        _registry.Register(new VanillaSlayMonster());
        _registry.Register(new VanillaFishing());

        // Seed the dispatch registry with the framework's built-in vanilla + RSV/ESV/VMV/SVE
        // entries. Goes through the same `Register` API third-party mods use, so there's
        // no privileged path.
        NpcDispatch.SeedBuiltins(Dispatch);

        // Resolve SpaceCore once so the API can forward `RegisterCustomQuestType` calls without
        // every consumer mod having to declare its own dependency on SpaceCore.
        _spaceCore = Helper.ModRegistry.GetApi<ISpaceCoreApi>(ModCompat.SpaceCore);
        if (_spaceCore != null)
        {
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsItemDeliveryQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsFishingQuest));
            _spaceCore.RegisterSerializerType(typeof(AdventureQuest));
            _spaceCore.RegisterSerializerType(typeof(MoreQuestsShipQuest));
            Monitor.Log("Registered framework Quest subclasses with SpaceCore.", LogLevel.Trace);
        }
        else
        {
            Monitor.Log(
                "SpaceCore not detected; framework Quest subclasses (item delivery, fishing, adventure) will not save. " +
                "Install SpaceCore for full functionality.",
                LogLevel.Warn);
        }

        // RegistrationOpen fires from OnFirstTick (one tick after every consumer mod's
        // GameLaunched runs), so consumer mods that subscribe in their own GameLaunched
        // — which by definition runs after the framework's, since they declare us as a
        // dependency — actually receive the event.
    }

    private void OnFirstTick(object? sender, UpdateTickingEventArgs e)
    {
        Helper.Events.GameLoop.UpdateTicking -= OnFirstTick;

        // Open the registration window. Consumer-mod handlers subscribed during their
        // own GameLaunched run synchronously here and call into IMoreQuestsModApi.
        _api.FireRegistrationOpen();

        // Auto-load any SMAPI content pack that targets the framework. Runs after
        // RegistrationOpen handlers, so C# generators consumer mods just registered
        // are visible when packs that reference them load.
        foreach (var pack in Helper.ContentPacks.GetOwned())
        {
            _loader.LoadContentPack(pack);
            _boardLoader.LoadContentPack(pack);
        }

        _api.FireRegistrationClosed();
        _registry.Freeze();
        _boards.Freeze();

        GmcmRegistration.Register(Helper, ModManifest, Config, _registry, onReset: () => Config = new MoreQuestsFrameworkConfig());
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _dataCache = new GameDataCache(Monitor);
        _dataCache.Refresh();

        var items = new ItemResolver(Monitor, _dataCache);
        var ctx = new QuestContext(Helper, Monitor, Config, items, _dataCache, Dispatch);
        _antiRepetition = new AntiRepetition();

        _stateStore = new StateStore(Helper.Data, Monitor);
        _stateStore.Load();

        _triggers = new TriggerEvaluator(_stateStore.State, Monitor);
        _pipeline = new QuestPipeline(ctx, _registry, _antiRepetition, _triggers);

        _poster!.WireMailDelivery(_mailQuests, _stateStore.State);
        _specialOrderWriter?.WireState(_stateStore.State);
        _shopDiscountWriter?.WireState(_stateStore.State);
        _animalPurchaseDiscountWriter?.WireState(_stateStore.State);
        _festivalBiasWriter?.WireState(_stateStore.State);
        // Always invalidate the shop cache after wiring state — discounts loaded from the
        // save would otherwise sit dormant until something else triggers the next read.
        if (_stateStore.State.ActiveShopDiscounts.Count > 0)
            Helper.GameContent.InvalidateCache("Data/Shops");
        // Re-publish any persisted SpecialOrder entries by invalidating the cache;
        // the writer's OnAssetRequested handler injects them on the next read.
        if (_stateStore.State.EmittedSpecialOrders.Count > 0)
            Helper.GameContent.InvalidateCache("Data/SpecialOrders");

        // Rehydrate any mail letters that were sitting unread when the previous
        // session saved. Re-injects bodies into the Data/mail edit and re-registers
        // each prepared Quest so the `%item quest %% ` token resolves on letter-open.
        _mailQuests.Clear();
        var mailbox = Game1.player?.mailbox;
        var mailReceived = Game1.player?.mailReceived;
        var stillPending = new List<StashedMailQuest>();
        foreach (var stash in _stateStore.State.PendingMailDeliveries)
        {
            bool inMailbox = mailbox != null && mailbox.Contains(stash.MailKey);
            bool alreadyRead = mailReceived != null && mailReceived.Contains(stash.MailKey);
            if (alreadyRead || !inMailbox)
                continue; // already opened or vanished — drop on next save
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
            Monitor.Log($"Rehydrated {stillPending.Count} unread mail-quest letter(s) from save state.", LogLevel.Trace);
        }

        _dialogueWatcher = new DialogueWatcher(
            _registry, ctx, _stateStore.State, _api, Monitor,
            posting => _poster!.PrepareQuest(posting, daysLeft: Math.Max(1, posting.DeadlineDays)));
        _dialogueWatcher.Reset();

        // Phase 9a: stand up the consequence engine + dialogue watcher per-save. Engine is
        // exposed via a static `Active` so quest-subclass `questComplete` overrides can fire
        // it from anywhere without threading an instance reference through every subclass.
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

    /// At day-end, before vanilla sells the shipping bin, walk every active framework
    /// quest looking for a `MoreQuestsShipQuest` or an `AdventureQuest` with an active
    /// `Ship` step and credit them with matching items in the bin. We only observe —
    /// items still get sold to the player at full price (the bin contents are not
    /// removed). Cheap when no ship quest is active: the loop short-circuits the first
    /// time it sees no candidate.
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

            // Lazily fetch the bin so we don't pay the cost when no ship-tracking quest is active.
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

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady || _pipeline == null || _poster == null)
            return;

        _dataCache?.Refresh();
        // Snapshot anti-repetition state so a same-day mq_refresh can roll back today's
        // cooldown records and produce a fresh batch instead of an empty board.
        _antiRepetition?.BeginDay();
        _triggers?.BeginDay();
        _poster.BeginDay();

        // Phase 9.5c: credit AdventureQuest `Build` steps with the building-types diff
        // computed by `_triggers.BeginDay()`. Same source of truth the BuildingBuilt
        // trigger uses, no extra farm scan.
        ObserveBuildOnQuestLog();

        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);
        _poster.CommitBoard();

        var triggered = _pipeline.GenerateTriggered();
        _poster.PostBatch(triggered);

        // Sweep expired SpecialOrder dict entries before emitting today's batch so a
        // re-fire on a yearly cadence doesn't collide with a stale entry from last year.
        _specialOrderWriter?.SweepExpired();
        var specialOrders = _pipeline.GenerateSpecialOrders();
        _poster.PostBatch(specialOrders);

        // Queue NpcDialogue-source quests so the watcher can push them into the
        // journal at the next chat with their target NPC.
        if (_dialogueWatcher != null)
        {
            foreach (var (def, npc) in _pipeline.GenerateNpcDialogueQueue())
                _dialogueWatcher.Enqueue(def.Id, npc);
        }

        // Custom-board postings (Phase 8c). Drawn per-board with each board's own
        // `AllowedCategories` filter + `PoolSize` cap. Slots stay scoped to their board
        // key; the BoardWorldRenderer's "!" indicator activates as soon as the list is
        // non-empty.
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

        // Suppress vanilla's lone questOfTheDay so we are the single source of truth on the board.
        if (Game1.IsMasterGame)
            Game1.netWorldState.Value.SetQuestOfTheDay(null);

        // Poll ReachLevel steps once per day so a quest accepted on a previous session,
        // where the player already descended past the target floor, advances without
        // requiring a fresh warp into the mine.
        ObserveReachLevelOnQuestLog();

        // Sweep expired ShopDiscount entries; invalidate the shop cache when something
        // dropped off so the asset edit picks up the smaller list.
        SweepShopDiscounts();
        _animalPurchaseDiscountWriter?.SweepExpired();

        // Sweep expired FestivalBias entries so the patches stay fast on saves where the
        // player accepted a feast quest months ago and never made it to the festival.
        _festivalBiasWriter?.SweepExpired();

        // Drop pending consequence dialogue lines past their grace window so chained
        // reactions don't sit in the queue indefinitely if the player ducks the NPC.
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
        int weeds = 0;
        foreach (var pair in e.Removed)
        {
            var obj = pair.Value;
            if (obj == null) continue;
            if (obj.IsWeeds())
                weeds++;
        }
        if (weeds == 0)
            return;
        string locName = e.Location.Name;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is AdventureQuest a && !a.completed.Value)
                a.ObserveWeedsCleared(locName, weeds);
        }
    }

    /// Walks the active quest log once and lets every `AdventureQuest` with an active
    /// `ReachLevel` step compare its target floor against the player's deepest reached
    /// mine/skull-cavern level. Cheap when no ReachLevel quest is active (just a type check
    /// per quest). Called from DayStarted and `Player.Warped`.
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

    /// Walks the active quest log once at DayStarted and lets every `AdventureQuest` with
    /// an active `Build` step credit against the building-types newly added to the farm
    /// since yesterday's snapshot. Diff is computed by `TriggerEvaluator.BeginDay`, so no
    /// extra farm scan here.
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

    /// Recomputes `DecorShippingPatches.ActiveCount` from the player's quest log.
    /// Called once a second alongside the dialogue / consequence / clump pollers. Counts
    /// every active framework quest (`AdventureQuest` with any decor-shipping step,
    /// `MoreQuestsShipQuest` with the flag set) so the gated `Object.canBeShipped`
    /// postfix can fast-path out when no decor-shipping quest is in the log.
    private static void RecomputeDecorShippingCount()
    {
        if (Game1.player == null)
        {
            Patches.DecorShippingPatches.ActiveCount = 0;
            return;
        }
        int count = 0;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value)
                continue;
            if (q is Quests.AdventureQuest a && a.HasDecorShippingStep)
                count++;
            else if (q is Quests.MoreQuestsShipQuest s && s.allowDecorShipping.Value)
                count++;
        }
        Patches.DecorShippingPatches.ActiveCount = count;
    }

    /// Walks the active quest log once a second and lets every `AdventureQuest` with an
    /// active `ClearDebris` step poll the resource-clump count at its target location.
    /// Cheap when no ClearDebris quest is active — early-returns on the per-step kind
    /// check before touching `location.resourceClumps`.
    private void PollClumpsOnQuestLog()
    {
        if (Game1.player == null)
            return;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not AdventureQuest a || a.completed.Value)
                continue;
            // Player.currentLocation is the most-likely spot for clump removal; polling
            // there is enough for the common case (player breaks a clump where they
            // stand). For multi-location ClearDebris quests the next warp re-baselines.
            a.PollResourceClumps(Game1.currentLocation);
        }
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

    /// Re-rolls the daily-board batch on demand. Used by `IMoreQuestsApi.RefreshOffers()`
    /// so testers can preview new variants without reloading the save. Safe to call at
    /// any time after save load — uses the same code path as the day-start flow.
    /// Test/debug helper. Force-fires every SpecialOrder-source definition regardless of
    /// today's date or cooldown, dropping any persisted emit records for those defs first.
    /// Bypasses `TriggerEvaluator.SpecialOrderReady` entirely so a save that already saw
    /// the trigger fire (LastFiredDay populated) can re-emit the entry without waiting
    /// for the next StartDate. Caller must open the SpecialOrders board afterwards to see
    /// the result.
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
            // Drop any persisted entry for this def so the writer can emit fresh.
            state.EmittedSpecialOrders.RemoveAll(e => e.DefinitionId == def.Id);
            // Clear cooldown bookkeeping so the trigger evaluator wouldn't block future fires.
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
        // Roll back today's cooldown records so just-posted definitions are eligible again
        // — otherwise the re-roll pool is empty and tomorrow's batch is also blocked.
        _antiRepetition?.RewindToDayStart();
        _poster.BeginDay();
        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);
        _poster.CommitBoard();
        Monitor.Log($"RefreshOffers: re-rolled {daily.Count} daily postings.", LogLevel.Info);
        _api.FireDayRefreshed(daily.Count, 0);
    }

    /// Diff the player's quest log against the previous tick to fire `QuestAccepted`
    /// (managed quest appeared in the log), `QuestCompleted` (completed.Value flipped
    /// to true), and `QuestRemoved` (managed quest left the log). Cheap because most
    /// ticks have no diff.
    private void OnOneSecondTick(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null)
            return;

        _dialogueWatcher?.Tick();
        _consequenceWatcher?.Tick();
        PollClumpsOnQuestLog();
        RecomputeDecorShippingCount();
        // Grant FrameworkRewards for any framework-emitted SpecialOrder that completed
        // this tick. Bypasses vanilla's Data/SpecialOrders Rewards array entirely so
        // third-party content packs that mutate that array can't intercept the grant.
        _specialOrderWriter?.CheckCompletionsAndGrantRewards();

        var current = Game1.player.questLog;
        var seenThisTick = new HashSet<Quest>();
        for (int i = 0; i < current.Count; i++)
        {
            var q = current[i];
            if (q == null)
                continue;
            if (!_api.TryGetManaged(q, out var info))
                continue;

            seenThisTick.Add(q);
            if (_seenInLog.Add(q))
            {
                _api.FireQuestAccepted(q, info);
                // The mail letter has been opened (vanilla addQuest pushed the quest
                // into the log); the stash + Data/mail edit are no longer needed.
                if (!string.IsNullOrEmpty(q.id.Value))
                    _poster?.DropStash(q.id.Value);
            }

            if (q.completed.Value && _completedFired.Add(q))
                _api.FireQuestCompleted(q, info);
        }

        if (_seenInLog.Count > 0)
        {
            var removed = new List<Quest>();
            foreach (var q in _seenInLog)
            {
                if (!seenThisTick.Contains(q))
                    removed.Add(q);
            }
            for (int i = 0; i < removed.Count; i++)
            {
                var q = removed[i];
                _seenInLog.Remove(q);
                bool wasCompleted = _completedFired.Remove(q) || q.completed.Value;
                if (_api.TryGetManaged(q, out var info))
                    _api.FireQuestRemoved(q, info, wasCompleted);
            }
        }
    }
}
