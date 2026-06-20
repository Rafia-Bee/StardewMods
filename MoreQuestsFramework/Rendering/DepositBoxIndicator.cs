using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace MoreQuestsFramework.Rendering;

// The bobbing "!" bubble vanilla floats over a special-order drop box, pulled out so any
// quest deposit box can reuse it. Call Draw from a Display.RenderedWorld handler for each
// tile that should read as "leave goods here". Same sprite and motion as the vanilla
// indicator (mouseCursors2, the little exclamation bubble).
public static class DepositBoxIndicator
{
    private const int TilePixels = 64;
    private static readonly Rectangle Source = new(114, 53, 6, 10);

    public static void Draw(SpriteBatch b, int tileX, int tileY)
    {
        float bob = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
        Vector2 pixel = new(tileX * TilePixels + 28f, tileY * TilePixels + bob);
        b.Draw(
            Game1.mouseCursors2,
            Game1.GlobalToLocal(Game1.viewport, pixel),
            Source,
            Color.White,
            0f,
            new Vector2(1f, 4f),
            4f,
            SpriteEffects.None,
            1f);
    }
}
