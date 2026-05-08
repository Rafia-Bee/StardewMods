using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace DeluxeGrabberFix.Framework;

/// <summary>
/// A toggleable "Global Grabber" button rendered on the auto-grabber's ItemGrabMenu.
/// Works on all platforms (PC, mobile, console) since it uses tap/click — no hover or hotkey required.
/// </summary>
internal class GlobalGrabberButton
{
    private readonly ModEntry _mod;
    private readonly Object _grabberObject;
    private readonly ClickableTextureComponent _button;
    private bool _isDesignated;

    // Quality star icons from cursors spritesheet
    private static readonly Rectangle GoldStarSource = new(346, 400, 8, 8);
    private static readonly Rectangle SilverStarSource = new(338, 400, 8, 8);

    public GlobalGrabberButton(ModEntry mod, Object grabberObject, IClickableMenu menu)
    {
        _mod = mod;
        _grabberObject = grabberObject;
        _isDesignated = grabberObject.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey);

        int offsetX = mod.Config.GlobalGrab.globalButtonOffsetX;
        int offsetY = mod.Config.GlobalGrab.globalButtonOffsetY;

        // Position: top-right area of the menu, near the organize button
        int x = menu.xPositionOnScreen + menu.width + offsetX;
        int y = menu.yPositionOnScreen + offsetY;

        _button = new ClickableTextureComponent(
            new Rectangle(x, y, 64, 64),
            Game1.mouseCursors,
            _isDesignated ? GoldStarSource : SilverStarSource,
            scale: 4f)
        {
            hoverText = _isDesignated
                ? _mod.Helper.Translation.Get("button.remove-global-grabber")
                : _mod.Helper.Translation.Get("button.set-global-grabber")
        };
    }

    public void Draw(SpriteBatch b)
    {
        _button.draw(b);

        if (_button.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
        {
            IClickableMenu.drawHoverText(b, _button.hoverText, Game1.smallFont);
        }
    }

    public bool TryClick(int x, int y)
    {
        if (!_button.containsPoint(x, y))
            return false;

        // The "smallSelect" click feedback is button-only; the keybind path is silent because
        // it has no menu-click context. Sharing the rest of the toggle (modData mutation,
        // ClearAllDesignations, HUD message) with HandleDesignateGrabber via
        // ToggleGrabberDesignation keeps the two entry points from drifting (audit §2.4).
        Game1.playSound("smallSelect");
        _isDesignated = _mod.ToggleGrabberDesignation(_grabberObject);

        _button.sourceRect = _isDesignated ? GoldStarSource : SilverStarSource;
        _button.hoverText = _isDesignated
            ? _mod.Helper.Translation.Get("button.remove-global-grabber")
            : _mod.Helper.Translation.Get("button.set-global-grabber");

        return true;
    }
}
