using Microsoft.Xna.Framework;
using QuestJournal.Integrations;
using StardewValley;
using StardewValley.Menus;

namespace QuestJournal.Menu;

// Adds a journal tab to the Better Game Menu when that mod is installed.
// Registers the tab with its icon, and when the player clicks it we open our
// standalone journal menu. The page itself is just an empty placeholder.
internal sealed class BgmIntegration
{
    internal const string TabId = "RafiaBee.QuestJournal/Journal";

    private const int Order = 180;

    private readonly IBetterGameMenuApi _api;
    private readonly System.Func<IClickableMenu?> _buildStandaloneJournal;
    private readonly System.Func<string> _displayName;
    private readonly Microsoft.Xna.Framework.Graphics.Texture2D _icon;

    public BgmIntegration(IBetterGameMenuApi api, System.Func<IClickableMenu?> buildStandaloneJournal, System.Func<string> displayName, Microsoft.Xna.Framework.Graphics.Texture2D icon)
    {
        _api = api;
        _buildStandaloneJournal = buildStandaloneJournal;
        _displayName = displayName;
        _icon = icon;
    }

    public void Register()
    {
        var iconDraw = _api.CreateDraw(
            _icon,
            new Rectangle(0, 0, 16, 16),
            scale: 3f,
            offset: new Vector2(0f, 2f));

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
