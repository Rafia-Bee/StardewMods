#nullable enable
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;

namespace UIInfoSuiteAddon.Framework;

/// <summary>
/// A thin on-screen menu registered with Game1.onScreenMenus so that
/// Lookup Anything can detect HoveredNpc when the player hovers a birthday icon.
/// </summary>
internal sealed class BirthdayLookupOverlay : IClickableMenu
{
    public NPC? HoveredNpc;

    public override bool isWithinBounds(int x, int y)
    {
        return HoveredNpc != null;
    }

    public override void draw(SpriteBatch b)
    {
    }
}
