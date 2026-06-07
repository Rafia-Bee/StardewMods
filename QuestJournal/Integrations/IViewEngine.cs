using System;
using StardewValley.Menus;

namespace QuestJournal.Integrations;

// Our copy of the StardewUI view engine API. We use it to build the journal menus from asset files.
public interface IViewEngine
{
    void RegisterSprites(string assetPrefix, string modDirectory);
    void RegisterViews(string assetPrefix, string modDirectory);
    void EnableHotReloading(string? sourceDirectory = null);
    void PreloadAssets();

    IClickableMenu CreateMenuFromAsset(string assetName, object? context = null);
    IMenuController CreateMenuControllerFromAsset(string assetName, object? context = null);
}

public interface IMenuController : IDisposable
{
    IClickableMenu Menu { get; }
    float DimmingAmount { get; set; }
    Func<Microsoft.Xna.Framework.Point> PositionSelector { get; set; }
    event Action Closed;
    void Close();
}
