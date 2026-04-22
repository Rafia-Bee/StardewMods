using HarmonyLib;
using MoreQuests.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuests;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    private QuestBoardManager? _questBoard;
    private ConsequenceManager? _consequences;
    private AnimalQuestTracker? _animalTracker;
    private FestivalQuestManager? _festivalQuests;
    private HelpWantedBridge? _helpWantedBridge;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        var harmony = new Harmony(ModManifest.UniqueID);
        QuestBoardPatches.Apply(harmony, Monitor);

        _helpWantedBridge = new HelpWantedBridge(helper, Monitor);
        _helpWantedBridge.Register();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        GmcmRegistration.Register(Helper, ModManifest);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        var itemResolver = new ItemResolver(Monitor);
        var difficultyScaler = new DifficultyScaler();
        var antiRepetition = new AntiRepetitionTracker();

        _questBoard = new QuestBoardManager(Helper, Monitor, itemResolver, difficultyScaler, antiRepetition);
        _consequences = new ConsequenceManager(Helper, Monitor);
        _animalTracker = new AnimalQuestTracker(Helper, Monitor);
        _festivalQuests = new FestivalQuestManager(Helper, Monitor, itemResolver, difficultyScaler);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        _questBoard?.GenerateDailyQuests();
        _festivalQuests?.CheckForFestivalQuests();
        _animalTracker?.CheckForTriggers();

        if (_questBoard != null)
            _helpWantedBridge?.SetDailyQuests(_questBoard.ActiveQuests);
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        _consequences?.ProcessDayEndConsequences();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        _questBoard?.SaveState();
    }
}
