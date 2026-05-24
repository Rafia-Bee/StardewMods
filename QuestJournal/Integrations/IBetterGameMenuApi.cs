using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.Menus;

namespace QuestJournal.Integrations;

// Local duck-typed mirror of leclair.bettergamemenu's IBetterGameMenuApi.
// Only the methods + nested types we actually use are declared. SMAPI's
// ModRegistry.GetApi proxies our interface against the real one; signatures
// must match exactly.
public interface ITabChangedEvent
{
    IClickableMenu Menu { get; }
    string Tab { get; }
    string OldTab { get; }
}

public interface IBetterGameMenuApi
{
    public delegate void DrawDelegate(SpriteBatch batch, Rectangle bounds);

    public delegate void TabChangedDelegate(ITabChangedEvent evt);

    DrawDelegate CreateDraw(Texture2D texture, Rectangle source, float scale = 1f, int frames = 1, int frameTime = 16, Vector2? offset = null);

    void RegisterTab(
        string id,
        int order,
        System.Func<string> getDisplayName,
        System.Func<(DrawDelegate DrawMethod, bool DrawBackground)> getIcon,
        int priority,
        System.Func<IClickableMenu, IClickableMenu> getPageInstance,
        System.Func<DrawDelegate?>? getDecoration = null,
        System.Func<bool>? getTabVisible = null,
        System.Func<bool>? getMenuInvisible = null,
        System.Func<int, int>? getWidth = null,
        System.Func<int, int>? getHeight = null,
        System.Func<(IClickableMenu Menu, IClickableMenu OldPage), IClickableMenu?>? onResize = null,
        System.Action<IClickableMenu>? onClose = null);

    void OnTabChanged(TabChangedDelegate handler, EventPriority priority = EventPriority.Normal);
    void OffTabChanged(TabChangedDelegate handler);
}
