using Microsoft.Xna.Framework;
using QuestJournal.Integrations;
using StardewValley;
using StardewValley.Menus;

namespace QuestJournal.Menu;

// Registers the journal as a tab on Better Game Menu's strip. Clicking the
// tab closes the BGM menu and opens the journal as a standalone activeMenu,
// matching the F6 hotkey path. BGM's RegisterTab still needs a non-null
// getPageInstance so we hand back a tiny throwaway page; in practice the
// TabChanged handler swaps activeClickableMenu out from under BGM before
// the placeholder is ever visible.
internal sealed class BgmIntegration
{
    internal const string TabId = "RafiaBee.QuestJournal/Journal";

    // Between Options (160) and Exit (200) so the journal sits on the right
    // side of the strip without being last.
    private const int Order = 180;

    private readonly IBetterGameMenuApi _api;
    private readonly System.Func<IClickableMenu?> _buildStandaloneJournal;
    private readonly System.Func<string> _displayName;

    public BgmIntegration(IBetterGameMenuApi api, System.Func<IClickableMenu?> buildStandaloneJournal, System.Func<string> displayName)
    {
        _api = api;
        _buildStandaloneJournal = buildStandaloneJournal;
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
            getPageInstance: CreatePlaceholderPage);

        _api.OnTabChanged(OnTabChanged);
    }

    private void OnTabChanged(ITabChangedEvent evt)
    {
        if (evt.Tab != TabId)
            return;
        var journal = _buildStandaloneJournal();
        if (journal != null)
            Game1.activeClickableMenu = journal;
    }

    // BGM requires a non-null page even though we immediately swap it out.
    // Use a borderless empty IClickableMenu sized to the BGM container so
    // there's no jarring flash in the frame between TabChanged firing and
    // activeClickableMenu being reassigned.
    private static IClickableMenu CreatePlaceholderPage(IClickableMenu container)
    {
        return new IClickableMenuPlaceholder(
            container.xPositionOnScreen,
            container.yPositionOnScreen,
            container.width,
            container.height);
    }

    private sealed class IClickableMenuPlaceholder : IClickableMenu
    {
        public IClickableMenuPlaceholder(int x, int y, int w, int h)
            : base(x, y, w, h)
        {
            allClickableComponents = new System.Collections.Generic.List<ClickableComponent>();
        }
        public override void draw(Microsoft.Xna.Framework.Graphics.SpriteBatch b) { }
    }
}
