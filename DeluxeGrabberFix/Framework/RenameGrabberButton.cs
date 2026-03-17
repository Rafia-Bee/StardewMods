using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace DeluxeGrabberFix.Framework;

internal class RenameGrabberButton
{
    private readonly ModEntry _mod;
    private readonly Object _grabberObject;
    private readonly ClickableComponent _button;
    private string _currentName;

    private const int ButtonWidth = 100;
    private const int ButtonHeight = 44;

    public RenameGrabberButton(ModEntry mod, Object grabberObject, IClickableMenu menu)
    {
        _mod = mod;
        _grabberObject = grabberObject;
        _currentName = ModEntry.GetGrabberCustomName(grabberObject);

        int offsetX = mod.Config.renameButtonOffsetX;
        int offsetY = mod.Config.renameButtonOffsetY;

        int x = menu.xPositionOnScreen + menu.width + offsetX;
        int y = menu.yPositionOnScreen + offsetY + 72;

        _button = new ClickableComponent(
            new Rectangle(x, y, ButtonWidth, ButtonHeight),
            "rename");
    }

    public void Draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(432, 439, 9, 9),
            _button.bounds.X, _button.bounds.Y,
            _button.bounds.Width, _button.bounds.Height,
            _button.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY())
                ? Color.Wheat
                : Color.White,
            4f);

        string label = _mod.Helper.Translation.Get("button.rename-grabber");
        Vector2 textSize = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b,
            label,
            Game1.smallFont,
            new Vector2(
                _button.bounds.X + (_button.bounds.Width - textSize.X) / 2,
                _button.bounds.Y + (_button.bounds.Height - textSize.Y) / 2),
            Game1.textColor);

        if (_button.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()) && !string.IsNullOrEmpty(_currentName))
        {
            IClickableMenu.drawHoverText(b, _currentName, Game1.smallFont);
        }
    }

    public bool TryClick(int x, int y)
    {
        if (!_button.containsPoint(x, y))
            return false;

        Game1.playSound("smallSelect");

        string defaultName = _currentName ?? "";
        Game1.activeClickableMenu = new NamingMenu(
            OnNameChosen,
            _mod.Helper.Translation.Get("naming.title"),
            defaultName)
        {
            minLength = 0
        };

        return true;
    }

    private void OnNameChosen(string name)
    {
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
            _grabberObject.modData.Remove(ModEntry.GrabberNameModDataKey);
        else
            _grabberObject.modData[ModEntry.GrabberNameModDataKey] = name;

        _currentName = string.IsNullOrEmpty(name) ? null : name;
        Game1.exitActiveMenu();
    }
}
