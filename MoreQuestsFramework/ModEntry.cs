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

        var harmony = new Harmony(ModManifest.UniqueID);
        BillboardPatches.Apply(harmony);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondTick;

        helper.ConsoleCommands.Add(
            "mq_refresh",
            "Re-rolls today's daily-board postings without reloading the save.",
            (_, _) => RefreshOffers());
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
            Monitor.Log("Registered framework Quest subclasses with SpaceCore.", LogLevel.Trace);
        }
        else
        {
            Monitor.Log(
                "SpaceCore not detected; framework Quest subclasses (item delivery, fishing) will not save. " +
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

        _pipeline = new QuestPipeline(ctx, _registry, _antiRepetition);

        _watching.Clear();
        _seenInLog.Clear();
        _completedFired.Clear();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady || _pipeline == null || _poster == null)
            return;

        _dataCache?.Refresh();
        // Snapshot anti-repetition state so a same-day mq_refresh can roll back today's
        // cooldown records and produce a fresh batch instead of an empty board.
        _antiRepetition?.BeginDay();
        _poster.BeginDay();

        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);
        _poster.CommitBoard();

        var triggered = _pipeline.GenerateTriggeredMail();
        _poster.PostBatch(triggered);

        // Suppress vanilla's lone questOfTheDay so we are the single source of truth on the board.
        if (Game1.IsMasterGame)
            Game1.netWorldState.Value.SetQuestOfTheDay(null);

        _api.FireDayRefreshed(daily.Count, triggered.Count);
    }

    /// Re-rolls the daily-board batch on demand. Used by `IMoreQuestsApi.RefreshOffers()`
    /// so testers can preview new variants without reloading the save. Safe to call at
    /// any time after save load — uses the same code path as the day-start flow.
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
                _api.FireQuestAccepted(q, info);

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
