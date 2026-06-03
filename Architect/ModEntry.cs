using Architect.Quests;
using Architect.Services;
using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace Architect;

public sealed class ModEntry : Mod
{
    internal static ITranslationHelper? Translation { get; private set; }

    private ModConfig _config = new();
    private readonly FurniturePlacementWatcher _watcher = new();

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        Translation = helper.Translation;

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.OneSecondUpdateTicked += _watcher.OnOneSecondUpdateTicked;
        helper.Events.Player.Warped += _watcher.OnWarped;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var fw = Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (fw == null)
        {
            Monitor.Log(
                "More Quests Framework not loaded; the Architect quest cannot register. " +
                "Install RafiaBee.MoreQuestsFramework to enable it.",
                LogLevel.Error);
            return;
        }

        fw.RegistrationOpen += (_, _) => RegisterContent(fw);
        fw.QuestAccepted += OnQuestAccepted;
        fw.QuestRemoved += OnQuestRemoved;
    }

    private void RegisterContent(IMoreQuestsApi fw)
    {
        var scope = fw.GetModApi(ModManifest);
        // Must register the subclass so an in-progress board quest round-trips through SpaceCore.
        scope.RegisterCustomQuestType(typeof(ArchitectQuest));
        scope.RegisterQuest(new ArchitectQuestDefinition(Helper, _config));
    }

    // Pays the budget the moment the quest lands in the log. Guarded so the reload-time
    // re-fire of QuestAccepted can't pay twice.
    private void OnQuestAccepted(object? sender, QuestAcceptedArgs e)
    {
        if (e.Quest is ArchitectQuest aq)
            aq.GrantBudgetOnAccept();
    }

    // Returns the unused budget if the quest leaves the log without completing
    // (cancelled or expired). Completion settles itself inside questComplete.
    private void OnQuestRemoved(object? sender, QuestRemovedArgs e)
    {
        if (e.Reason != QuestRemovalReason.Completed && e.Quest is ArchitectQuest aq)
            aq.Settle();
    }
}
