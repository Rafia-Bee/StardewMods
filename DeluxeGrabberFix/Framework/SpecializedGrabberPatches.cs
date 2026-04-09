using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.Tools;

namespace DeluxeGrabberFix.Framework;

internal static class SpecializedGrabberPatches
{
    internal const string ModDataGrabberType = "Rafia.DGF/GrabberType";
    internal const string ModDataOriginalId = "Rafia.DGF/OriginalId";

    internal static bool MinutesElapsed_Prefix(Object __instance)
    {
        if (__instance.QualifiedItemId != BigCraftableIds.AutoGrabber)
            return true;

        // Specialized grabbers skip vanilla minutesElapsed to prevent
        // unwanted animal product collection and readyForHarvest flags
        if (__instance.modData.ContainsKey(ModDataGrabberType))
            return false;

        return true;
    }

    internal static bool Draw_Prefix(Object __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        if (!__instance.bigCraftable.Value) return true;
        if (__instance.QualifiedItemId != BigCraftableIds.AutoGrabber) return true;
        if (!__instance.modData.TryGetValue(ModDataOriginalId, out string originalId)) return true;
        if (__instance.isTemporarilyInvisible) return false;

        if (__instance.hovering)
            __instance.hovering = false;

        ParsedItemData customData = ItemRegistry.GetDataOrErrorItem(originalId);

        Vector2 scaleV = __instance.getScale() * 4f;
        Vector2 pos = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 64));
        Rectangle dest = new Rectangle(
            (int)(pos.X - scaleV.X / 2f) + (__instance.shakeTimer > 0 ? Game1.random.Next(-1, 2) : 0),
            (int)(pos.Y - scaleV.Y / 2f) + (__instance.shakeTimer > 0 ? Game1.random.Next(-1, 2) : 0),
            (int)(64f + scaleV.X),
            (int)(128f + scaleV.Y / 2f));
        float layerDepth = Math.Max(0f, (float)((y + 1) * 64 - 24) / 10000f) + x * 1E-05f;

        int offset = __instance.showNextIndex.Value ? 1 : 0;

        spriteBatch.Draw(
            customData.GetTexture(),
            dest,
            customData.GetSourceRect(offset),
            Color.White * alpha,
            0f, Vector2.Zero, SpriteEffects.None, layerDepth);

        return false;
    }

    internal static bool PerformToolAction_Prefix(Object __instance, Tool t, ref bool __result)
    {
        if (__instance.QualifiedItemId != BigCraftableIds.AutoGrabber) return true;
        if (!__instance.modData.TryGetValue(ModDataOriginalId, out string originalId)) return true;

        // Can't break while chest has items (same as vanilla auto-grabber)
        if (__instance.heldObject.Value is Chest chest && !chest.isEmpty())
        {
            chest.clearNulls();
            if (t != null && t.isHeavyHitter() && !(t is MeleeWeapon))
            {
                __instance.playNearbySoundAll("hammer");
                __instance.shakeTimer = 100;
            }
            __result = false;
            return false;
        }

        // Empty or no chest: allow breaking, drop the custom grabber item
        __instance.heldObject.Value = null;
        __instance.ItemId = originalId.Replace("(BC)", "");
        return true;
    }
}
