using System;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace BiggerAutoGrabber.Framework;

internal static class ChestPatches
{
    internal const string CapacityKey = "BiggerAutoGrabber/Capacity";

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(Chest), nameof(Chest.GetActualCapacity)),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(GetActualCapacity_Postfix))
        );
    }

    private static void GetActualCapacity_Postfix(Chest __instance, ref int __result)
    {
        if (__instance.modData.TryGetValue(CapacityKey, out string val)
            && int.TryParse(val, out int cap)
            && cap > 0)
        {
            __result = cap;
        }
    }

    /// <summary>Finds the auto-grabber chest backing the given menu, if any.</summary>
    public static Chest FindAutoGrabberChest(ItemGrabMenu menu)
    {
        if (menu.context is StardewValley.Object obj
            && obj.bigCraftable.Value
            && obj.ParentSheetIndex == 165
            && obj.heldObject.Value is Chest chest)
        {
            return chest;
        }

        if (Game1.currentLocation == null)
            return null;

        var sourceItems = menu.ItemsToGrabMenu?.actualInventory;
        if (sourceItems == null)
            return null;

        foreach (var locObj in Game1.currentLocation.Objects.Values)
        {
            if (locObj.bigCraftable.Value
                && locObj.ParentSheetIndex == 165
                && locObj.heldObject.Value is Chest c
                && ReferenceEquals(c.Items, sourceItems))
            {
                return c;
            }
        }

        return null;
    }

    /// <summary>Resizes the menu's upper inventory to fit the given capacity.</summary>
    public static void ResizeMenu(ItemGrabMenu menu, int cap)
    {
        const int cols = 12;
        int desiredRows = (int)Math.Ceiling(cap / (double)cols);
        int visibleRows = Math.Min(desiredRows, 6);
        int defaultRows = menu.ItemsToGrabMenu.rows;
        int extraRows = visibleRows - defaultRows;

        if (extraRows <= 0)
            return;

        int shift = extraRows * 64;
        int visibleCap = visibleRows * cols;
        var old = menu.ItemsToGrabMenu;

        menu.ItemsToGrabMenu = new InventoryMenu(
            old.xPositionOnScreen,
            old.yPositionOnScreen,
            playerInventory: false,
            old.actualInventory,
            capacity: visibleCap,
            rows: visibleRows
        );

        menu.height += shift;
        menu.inventory.movePosition(0, shift);

        if (menu.okButton != null)
            menu.okButton.bounds.Y += shift;
        if (menu.trashCan != null)
            menu.trashCan.bounds.Y += shift;
        if (menu.organizeButton != null)
            menu.organizeButton.bounds.Y += shift;
        if (menu.fillStacksButton != null)
            menu.fillStacksButton.bounds.Y += shift;
        if (menu.colorPickerToggleButton != null)
            menu.colorPickerToggleButton.bounds.Y += shift;
        if (menu.junimoNoteIcon != null)
            menu.junimoNoteIcon.bounds.Y += shift;
    }
}
