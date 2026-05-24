using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QuestJournal.Integrations;
using StardewValley;
using StardewValley.Menus;

namespace QuestJournal.Menu;

// Registers the journal as a proper Better Game Menu tab so it sits next to
// the vanilla page tabs and inherits BGM's layout / controller / overflow
// behaviour. Falls back to the vanilla GameMenuTabOverlay path when BGM
// isn't loaded.
internal sealed class BgmIntegration
{
    private const string TabId = "RafiaBee.QuestJournal/Journal";

    // Between Options (160) and Exit (200) so the journal sits on the right
    // side of the strip without being last.
    private const int Order = 180;

    private readonly IBetterGameMenuApi _api;
    private readonly System.Func<IClickableMenu, IClickableMenu?> _createPage;
    private readonly System.Func<string> _displayName;

    public BgmIntegration(IBetterGameMenuApi api, System.Func<IClickableMenu, IClickableMenu?> createPage, System.Func<string> displayName)
    {
        _api = api;
        _createPage = createPage;
        _displayName = displayName;
    }

    public void Register()
    {
        // Vanilla quest-log scroll on mouseCursors. Placeholder until step 13's
        // art pass swaps in a dedicated 16x16 tab icon.
        var iconDraw = _api.CreateDraw(
            Game1.mouseCursors,
            new Rectangle(80, 0, 16, 16),
            scale: 1f);

        _api.RegisterTab(
            id: TabId,
            order: Order,
            getDisplayName: _displayName,
            getIcon: () => (iconDraw, /* DrawBackground */ true),
            priority: 0,
            getPageInstance: CreateInstance,
            onResize: OnResize);
    }

    private IClickableMenu CreateInstance(IClickableMenu container)
    {
        // StardewUI is a hard dep, so _createPage should always return a
        // menu. If something is catastrophically wrong, hand BGM the
        // container back so it doesn't blow up (it'll show an empty page).
        var page = _createPage(container) ?? container;
        AlignToContainer(page, container);
        return page;
    }

    private IClickableMenu? OnResize((IClickableMenu Menu, IClickableMenu OldPage) input)
    {
        var (container, page) = input;
        AlignToContainer(page, container);
        return page;
    }

    private static void AlignToContainer(IClickableMenu page, IClickableMenu container)
    {
        page.xPositionOnScreen = container.xPositionOnScreen;
        page.yPositionOnScreen = container.yPositionOnScreen;
        page.width = container.width;
        page.height = container.height;
        // BGM appends its own prev/next tab-row buttons into the page's
        // allClickableComponents list at TryChangeTab time. StardewUI menus
        // leave that list null, which makes BGM NRE on click. Pre-allocate.
        page.allClickableComponents ??= new System.Collections.Generic.List<ClickableComponent>();
    }
}
