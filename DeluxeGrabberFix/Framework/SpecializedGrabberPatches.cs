using System;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace DeluxeGrabberFix.Framework;

internal static class SpecializedGrabberPatches
{
    private static readonly System.Reflection.MethodInfo GrabItemMethod =
        AccessTools.Method(typeof(Object), "grabItemFromAutoGrabber");
    internal static bool MinutesElapsed_Prefix(Object __instance)
    {
        if (__instance.QualifiedItemId == BigCraftableIds.AutoGrabber)
            return true;

        if (GrabberTypeHelper.IsGrabber(__instance.QualifiedItemId))
            return false;

        return true;
    }

    internal static bool CheckForAction_Prefix(Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        if (__instance.QualifiedItemId == BigCraftableIds.AutoGrabber)
            return true;

        if (!GrabberTypeHelper.IsGrabber(__instance.QualifiedItemId))
            return true;

        if (justCheckingForActivity)
        {
            __result = true;
            return false;
        }

        if (__instance.heldObject.Value is Chest chest && !chest.isEmpty())
        {
            Game1.activeClickableMenu = new ItemGrabMenu(
                chest.Items,
                reverseGrab: false,
                showReceivingMenu: true,
                InventoryMenu.highlightAllItems,
                chest.grabItemFromInventory,
                null,
                (ItemGrabMenu.behaviorOnItemSelect)Delegate.CreateDelegate(
                    typeof(ItemGrabMenu.behaviorOnItemSelect), __instance, GrabItemMethod),
                snapToBottom: false,
                canBeExitedWithKey: true,
                playRightClickSound: true,
                allowRightClick: true,
                showOrganizeButton: true,
                1,
                null,
                -1,
                __instance);
            __result = true;
            return false;
        }

        __result = false;
        return false;
    }
}
