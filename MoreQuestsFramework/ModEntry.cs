using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Patches;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Quests.Vanilla;
using MoreQuestsFramework.Registry;
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

    internal DispatchRegistry Dispatch { get; private set; } = null!;
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
        Dispatch = new DispatchRegistry(helper.ModRegistry, Monitor);
        _api = new MoreQuestsApi(_registry, _generators, _loader, Dispatch, Monitor, () => _spaceCore, RefreshOffers);

        _poster = new QuestPoster(helper, Monitor, _api);
        _poster.Register();

        _specialOrderWriter = new SpecialOrderWriter(helper, Monitor);
        _specialOrderWriter.Register();
        _poster.WireSpecialOrders(_specialOrderWriter);

        _mailQuests = new MailQuestRegistry();

        var harmony = new Harmony(ModManifest.UniqueID);
        BillboardPatches.Apply(harmony);
        MailQuestPatches.Apply(harmony, _mailQuests, _api, Monitor);
        AdventureQuestPatches.Apply(harmony);
        SpecialOrdersBoardPatches.Apply(harmony, Monitor, _specialOrderWriter);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondTick;

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
            _loader.LoadContentPack(pack);

        _api.FireRegistrationClosed();
        _registry.Freeze();

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

        _watching.Clear();
        _seenInLog.Clear();
        _completedFired.Clear();
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

        // Suppress vanilla's lone questOfTheDay so we are the single source of truth on the board.
        if (Game1.IsMasterGame)
            Game1.netWorldState.Value.SetQuestOfTheDay(null);

        _api.FireDayRefreshed(daily.Count, triggered.Count);
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
