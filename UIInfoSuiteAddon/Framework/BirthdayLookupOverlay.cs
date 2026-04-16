#nullable enable
using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;

namespace UIInfoSuiteAddon.Framework;

/// <summary>
/// A thin on-screen menu registered with Game1.onScreenMenus so that
/// Lookup Anything can detect HoveredNpc when the player hovers a birthday icon.
/// Also draws a tooltip showing the NPC's loved gifts.
/// </summary>
internal sealed class BirthdayLookupOverlay : IClickableMenu
{
    public NPC? HoveredNpc;
    internal Func<ModConfig>? GetConfig;
    internal Func<string, string>? Translate;

    public override bool isWithinBounds(int x, int y)
    {
        return HoveredNpc != null;
    }

    public override void draw(SpriteBatch b)
    {
        if (HoveredNpc == null || GetConfig == null || Translate == null)
            return;

        var config = GetConfig();
        var gifts = GiftHelper.GetLovedGiftNames(HoveredNpc, config.MaxLovedGiftsToShow, config.ExcludeUniversalLoves, config.OnlyShowOwnedGifts);
        if (gifts.Count == 0)
            return;

        string title = Translate("tooltip.loved-gifts");
        string body = string.Join("\n", gifts);

        drawHoverText(b, body, Game1.smallFont, boldTitleText: title);
    }
}
