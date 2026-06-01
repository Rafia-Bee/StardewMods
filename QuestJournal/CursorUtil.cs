using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace QuestJournal;

internal static class CursorUtil
{
    // SMAPI's ICursorPosition.GetScaledScreenPixels() only applies the UI-scale
    // adjustment while the game is drawing UI (Game1.uiMode). Input events fire
    // outside that render pass, so it hands the cursor back in zoom space instead.
    // Our HUD and menu rects are built during rendering, so they live in UI space.
    // The two only line up when UI scale equals zoom (the 100% case), so at any
    // other UI scale a click landed on the wrong spot. Doing the transform here
    // keeps the cursor in UI space no matter when we read it.
    public static Vector2 UiSpace(ICursorPosition cursor)
        => Utility.ModifyCoordinatesForUIScale(cursor.ScreenPixels);

    public static Point UiSpacePoint(ICursorPosition cursor)
        => UiSpace(cursor).ToPoint();
}
