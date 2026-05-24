using StardewValley.Menus;

namespace QuestJournal.Integrations;

// Local mirror of focustense.StardewUI's IViewEngine. SMAPI's ModRegistry.GetApi
// duck-types our interface against the real one, so we don't take a project /
// assembly reference to StardewUI. Only methods we actually use are declared.
public interface IViewEngine
{
    void RegisterSprites(string assetPrefix, string modDirectory);
    void RegisterViews(string assetPrefix, string modDirectory);
    void EnableHotReloading(string? sourceDirectory = null);
    void PreloadAssets();

    IClickableMenu CreateMenuFromAsset(string assetName, object? context = null);
}
