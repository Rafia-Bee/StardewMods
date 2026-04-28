using MoreQuests.Quests;
using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MoreQuests;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static ModConfig Config { get; set; } = new();

    /// Static accessor used by content-mod quest definitions to look up their own i18n
    /// strings. The framework's `QuestContext.Helper` belongs to the framework, so its
    /// translation helper would only see the framework's i18n keys.
    internal static ITranslationHelper I18n => Instance.Helper.Translation;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var fw = Helper.ModRegistry.GetApi<IInternalApi>("RafiaBee.MoreQuestsFramework");
        if (fw == null)
        {
            Monitor.Log(
                "More Quests Framework not loaded; this mod cannot register its quests. " +
                "Install RafiaBee.MoreQuestsFramework to enable More Quests content.",
                LogLevel.Error);
            return;
        }

        // Register custom Quest subclasses with SpaceCore (via framework) so saves round-trip.
        fw.RegisterCustomQuestType(typeof(AnySlimeQuest));
        fw.RegisterCustomQuestType(typeof(CollectAndReportQuest));
        fw.RegisterCustomQuestType(typeof(CheckOnGeorgeQuest));

        // Register every content-mod quest definition.
        fw.RegisterQuest(new BasicCropDelivery());
        fw.RegisterQuest(new SimpleFishingRequest());
        fw.RegisterQuest(new BasicSlimeClearing());
        fw.RegisterQuest(new BarDelivery());
        fw.RegisterQuest(new SeasonalForaging());
        fw.RegisterQuest(new ElliottPoemInspiration());
        fw.RegisterQuest(new CheckOnGeorge());
        fw.RegisterQuest(new HaySupplyRun());
        fw.RegisterQuest(new BeachCleanup());
        fw.RegisterQuest(new SpringTea());
        fw.RegisterQuest(new CravingDish());

        GmcmRegistration.Register(Helper, ModManifest);
    }
}
