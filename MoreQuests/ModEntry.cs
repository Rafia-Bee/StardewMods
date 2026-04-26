using MoreQuests.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuests;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    private QuestPipeline? _pipeline;
    private QuestPoster? _poster;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        _poster = new QuestPoster(helper, Monitor);
        _poster.Register();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
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

        var daily = _pipeline.GenerateDailyPostings();
        _poster.PostBatch(daily);

        var triggered = _pipeline.GenerateTriggeredMail();
        _poster.PostBatch(triggered);
    }
}
