using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuests.Framework;
using MoreQuests.Framework.Cache;
using MoreQuests.Framework.Patches;
using MoreQuests.Framework.Quests;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuests;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    internal const string PadAssetRoot = "Mods/RafiaBee.MoreQuests/Pad";
    internal const string PinAssetRoot = "Mods/RafiaBee.MoreQuests/Pin";

    private QuestPipeline? _pipeline;
    private QuestPoster? _poster;
    private GameDataCache? _dataCache;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        _poster = new QuestPoster(helper, Monitor);
        _poster.Register();

        var harmony = new Harmony(ModManifest.UniqueID);
        BillboardPatches.Apply(harmony);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
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
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        BoardQuestRegistry.Initialize(Monitor);
        GmcmRegistration.Register(Helper, ModManifest);

        // Register our custom Quest subclasses with SpaceCore's serializer factory so
        // saving doesn't blow up on a `Quest` type the vanilla XmlSerializer can't see.
        var spaceCore = Helper.ModRegistry.GetApi<ISpaceCoreApi>(ModCompat.SpaceCore);
        if (spaceCore != null)
        {
            spaceCore.RegisterSerializerType(typeof(AnySlimeQuest));
            spaceCore.RegisterSerializerType(typeof(CollectAndReportQuest));
            spaceCore.RegisterSerializerType(typeof(CheckOnGeorgeQuest));
            spaceCore.RegisterSerializerType(typeof(MoreQuestsItemDeliveryQuest));
            spaceCore.RegisterSerializerType(typeof(MoreQuestsFishingQuest));
            Monitor.Log("Registered custom quest types with SpaceCore.", LogLevel.Trace);
        }
        else
        {
            Monitor.Log(
                "SpaceCore not detected; quests using custom subclasses (slime, beach, George, fishing) will not save. " +
                "Install SpaceCore for full functionality.",
                LogLevel.Warn);
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _dataCache = new GameDataCache(Monitor);
        _dataCache.Refresh();

        var items = new ItemResolver(Monitor, _dataCache);
        var ctx = new QuestContext(Helper, Monitor, Config, items, _dataCache);
        var antiRepetition = new AntiRepetition();

        _pipeline = new QuestPipeline(ctx, antiRepetition);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady || _pipeline == null || _poster == null)
            return;

        _dataCache?.Refresh();
        _poster.BeginDay();

        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);
        _poster.CommitBoard();

        var triggered = _pipeline.GenerateTriggeredMail();
        _poster.PostBatch(triggered);

        // Suppress vanilla's lone questOfTheDay so we are the single source of truth on the board.
        if (Game1.IsMasterGame)
            Game1.netWorldState.Value.SetQuestOfTheDay(null);
    }
}
