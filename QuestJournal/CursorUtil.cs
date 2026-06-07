using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace QuestJournal;

// Small helper to convert the mouse cursor position into UI space (accounting for UI scale).
internal static class CursorUtil
{
    public static Vector2 UiSpace(ICursorPosition cursor)
        => Utility.ModifyCoordinatesForUIScale(cursor.ScreenPixels);

    public static Point UiSpacePoint(ICursorPosition cursor)
        => UiSpace(cursor).ToPoint();
}
