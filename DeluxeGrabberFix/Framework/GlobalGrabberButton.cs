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

        int offsetX = mod.Config.globalButtonOffsetX;
        int offsetY = mod.Config.globalButtonOffsetY;

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

        Game1.playSound("smallSelect");

        if (_isDesignated)
        {
            _grabberObject.modData.Remove(ModEntry.GlobalGrabberModDataKey);
            _isDesignated = false;
            Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.no-longer-global")));
        }
        else
        {
            ClearAllDesignations();
            _grabberObject.modData[ModEntry.GlobalGrabberModDataKey] = "true";
            _isDesignated = true;
            Game1.addHUDMessage(new HUDMessage(_mod.Helper.Translation.Get("hud.now-global")));
        }

        _button.sourceRect = _isDesignated ? GoldStarSource : SilverStarSource;
        _button.hoverText = _isDesignated
            ? _mod.Helper.Translation.Get("button.remove-global-grabber")
            : _mod.Helper.Translation.Get("button.set-global-grabber");

        return true;
    }

    private void ClearAllDesignations()
    {
        foreach (var location in ModEntry.GetAllLocations())
        {
            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey))
                    pair.Value.modData.Remove(ModEntry.GlobalGrabberModDataKey);
            }
        }
    }
}
