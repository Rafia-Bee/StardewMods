using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace QuestJournal.Menu;

// Floats an extra tab to the right of the GameMenu's last vanilla tab, drawn
// every RenderedActiveMenu. Click hides the GameMenu and shows the journal as
// the new active menu. A MenuChanged subscription watches for the journal
// closing and restores the hidden GameMenu underneath. Doesn't touch
// GameMenu.tabs / pages so other tab-adding mods aren't disturbed.
internal sealed class GameMenuTabOverlay
{
    private readonly IModHelper _helper;
    private readonly System.Func<IClickableMenu?> _openJournal;
    private readonly ClickableComponent _tab;
    private readonly Texture2D _icon;

    private GameMenu? _hiddenMenu;
    private IClickableMenu? _activeJournal;

    public GameMenuTabOverlay(IModHelper helper, System.Func<IClickableMenu?> openJournal, string tooltip, Texture2D icon)
    {
        _helper = helper;
        _openJournal = openJournal;
        _tab = new ClickableComponent(new Rectangle(0, 0, 64, 64), "questJournal", tooltip);
        _icon = icon;
    }

    public void Register()
    {
        _helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        _helper.Events.Input.ButtonPressed += OnButtonPressed;
        _helper.Events.Display.MenuChanged += OnMenuChanged;
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (_activeJournal != null) return;
        if (Game1.activeClickableMenu is not GameMenu gameMenu)
            return;
        if (gameMenu.GetChildMenu() != null)
            return;
        if (e.Button != SButton.MouseLeft && e.Button != SButton.ControllerA)
            return;
        if (!_tab.bounds.Contains(e.Cursor.GetScaledScreenPixels().ToPoint()))
            return;

        var journal = _openJournal();
        if (journal == null)
            return;

        _hiddenMenu = gameMenu;
        gameMenu.invisible = true;
        gameMenu.upperRightCloseButton.visible = false;
        _activeJournal = journal;
        Game1.activeClickableMenu = journal;
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (_activeJournal == null || _hiddenMenu == null)
            return;
        if (ReferenceEquals(e.OldMenu, _activeJournal))
        {
            var gameMenu = _hiddenMenu;
            _hiddenMenu = null;
            _activeJournal = null;
            gameMenu.invisible = false;
            gameMenu.upperRightCloseButton.visible = true;
            if (e.NewMenu == null)
                Game1.activeClickableMenu = gameMenu;
        }
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not GameMenu gameMenu)
            return;
        if (gameMenu.invisible)
            return;
        if (gameMenu.GetChildMenu() != null)
            return;
        if (gameMenu.pages.Count > gameMenu.currentTab)
        {
            switch (gameMenu.pages[gameMenu.currentTab])
            {
                case MapPage:
                case CollectionsPage cp when cp.letterviewerSubMenu != null:
                    return;
            }
        }

        // Position right of the actual rightmost vanilla tab. Hardcoding
        // "tabs.Count * 64" off xPositionOnScreen breaks with mods that add
        // tabs at non-uniform widths or that draw tabs on the side rather
        // than the top. If for some reason no tabs are populated, fall back
        // to the menu's top-right corner.
        int rightmostRight = gameMenu.xPositionOnScreen;
        int rowY = gameMenu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY;
        bool found = false;
        foreach (var t in gameMenu.tabs)
        {
            if (t == null) continue;
            if (t.bounds.Right > rightmostRight)
            {
                rightmostRight = t.bounds.Right;
                rowY = t.bounds.Y;
                found = true;
            }
        }
        if (!found)
            rowY = gameMenu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY;
        _tab.bounds = new Rectangle(rightmostRight + 8, rowY, 64, 64);

        // If the calculated X would land off the right side of the screen
        // (too many mods appended tabs), drop the overlay so it's at least
        // not invisibly clickable. Better Game Menu integration (proper API)
        // lands in a later polish pass.
        if (_tab.bounds.X + _tab.bounds.Width > Game1.uiViewport.Width)
        {
            if (!_loggedOffscreen)
            {
                _loggedOffscreen = true;
                ModEntry.DebugLog(
                    $"Quest Journal tab would render off-screen (calculated X={_tab.bounds.X}, viewport width={Game1.uiViewport.Width}). Use the F6 hotkey instead. Better Game Menu / tab-API integration is coming later.",
                    LogLevel.Info);
            }
            return;
        }

        var batch = e.SpriteBatch;
        batch.Draw(
            _icon,
            new Vector2(_tab.bounds.X, _tab.bounds.Y),
            new Rectangle(0, 0, 16, 16),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            0.0001f);

        gameMenu.drawMouse(batch);

        if (_tab.bounds.Contains(_helper.Input.GetCursorPosition().GetScaledScreenPixels().ToPoint()))
        {
            IClickableMenu.drawHoverText(batch, _tab.label, Game1.smallFont);
        }
    }

    private bool _loggedOffscreen;
}
