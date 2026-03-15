#nullable enable
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;

namespace CatchOfTheDay.Framework;

/// <summary>
/// A thin on-screen menu registered with Game1.onScreenMenus so that
/// Lookup Anything can detect HoveredItem when the player hovers a fish icon.
/// </summary>
internal sealed class FishHudOverlay : IClickableMenu
{
    public Item? HoveredItem;

    public override bool isWithinBounds(int x, int y)
    {
        return HoveredItem != null;
    }

    public override void draw(SpriteBatch b)
    {
        // Drawing is handled by WeatherFishHud
    }
}
