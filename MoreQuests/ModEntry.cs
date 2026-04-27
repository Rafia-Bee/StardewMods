using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuests.Framework;
using MoreQuests.Framework.Patches;
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
        GmcmRegistration.Register(Helper, ModManifest);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        var items = new ItemResolver(Monitor);
        var ctx = new QuestContext(Helper, Monitor, Config, items);
        var antiRepetition = new AntiRepetition();

        _pipeline = new QuestPipeline(ctx, antiRepetition);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady || _pipeline == null || _poster == null)
            return;

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
