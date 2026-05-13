using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ExampleCSharpIQuestDef;

/// Pattern C: pure C# with no JSON. The quest is an `IQuestDefinition` instance
/// registered directly via `scope.RegisterQuest(...)`.
///
/// Use this pattern when:
/// - your quest list is short enough that JSON is overhead
/// - you want compile-time checking on the trigger, category, and reward shape
/// - you'd rather not ship a separate `.json` asset
///
/// Trade-off: the per-quest GMCM weight slider that the framework auto-generates
/// from JSON metadata won't appear. If you want config sliders, prefer pattern B.
public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var fw = this.Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (fw == null)
        {
            this.Monitor.Log("More Quests Framework not loaded. Quest disabled.", LogLevel.Warn);
            return;
        }

        fw.RegistrationOpen += (_, _) =>
        {
            var scope = fw.GetModApi(this.ModManifest);
            scope.RegisterQuest(new AppleForLewisDefinition(this.Helper));
        };
    }
}
