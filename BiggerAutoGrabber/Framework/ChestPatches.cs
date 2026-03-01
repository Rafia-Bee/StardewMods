using System;
using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace BiggerAutoGrabber.Framework;

internal static class ChestPatches
{
    internal const string CapacityKey = "BiggerAutoGrabber/Capacity";

    /// <summary>
    /// When true, <see cref="GetActualCapacity_Postfix"/> returns vanilla
    /// values so the <see cref="ItemGrabMenu"/> constructor builds a
    /// standard layout that <c>SetupBorderNeighbors()</c> can handle.
    /// </summary>
    [ThreadStatic]
    private static bool _suppressCapacity;

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(Chest), nameof(Chest.GetActualCapacity)),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(GetActualCapacity_Postfix))
        );

        // Patch the full ItemGrabMenu constructor (18 parameters) so we
        // can suppress GetActualCapacity during construction, then resize
        // safely in the finalizer once the base layout is complete.
        var ctor = AccessTools.GetDeclaredConstructors(typeof(ItemGrabMenu))
            .FirstOrDefault(c => c.GetParameters().Length >= 18);

        if (ctor != null)
        {
            harmony.Patch(
                original: ctor,
                prefix: new HarmonyMethod(typeof(ChestPatches), nameof(ItemGrabMenuCtor_Prefix)),
                finalizer: new HarmonyMethod(typeof(ChestPatches), nameof(ItemGrabMenuCtor_Finalizer))
            );
        }

        // Prevent grabItemFromChest / grabItemFromInventory from
        // rebuilding the entire ItemGrabMenu after every grab/deposit.
        harmony.Patch(
            original: AccessTools.Method(typeof(Chest), nameof(Chest.grabItemFromChest)),
            prefix: new HarmonyMethod(typeof(ChestPatches), nameof(GrabItemFromChest_Prefix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(Chest), nameof(Chest.grabItemFromInventory)),
            prefix: new HarmonyMethod(typeof(ChestPatches), nameof(GrabItemFromInventory_Prefix))
        );

        // Prevent Object.grabItemFromAutoGrabber from rebuilding the
        // entire ItemGrabMenu after every item withdrawal.
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Object), "grabItemFromAutoGrabber"),
            prefix: new HarmonyMethod(typeof(ChestPatches), nameof(GrabItemFromAutoGrabber_Prefix))
        );
    }

    // ── ItemGrabMenu constructor prefix / finalizer ─────────────────

    private static void ItemGrabMenuCtor_Prefix(Item sourceItem)
    {
        bool isOurs = sourceItem is Chest c && c.modData.ContainsKey(CapacityKey);
        if (isOurs)
            _suppressCapacity = true;
    }

    private static Exception ItemGrabMenuCtor_Finalizer(
        ItemGrabMenu __instance, Exception __exception, Item sourceItem)
    {
        bool wasSuppressed = _suppressCapacity;
        _suppressCapacity = false;

        if (__exception != null)
            return __exception;

        if (!wasSuppressed)
            return null;

        // The constructor completed with a vanilla layout. Now resize.
        var chest = FindAutoGrabberChest(__instance);
        if (chest == null && sourceItem is Chest src
            && src.modData.ContainsKey(CapacityKey))
        {
            chest = src;
        }

        if (chest != null
            && chest.modData.TryGetValue(CapacityKey, out string capStr)
            && int.TryParse(capStr, out int cap)
            && cap > 36)
        {
            ResizeMenu(__instance, cap);
        }

        return null;
    }

    // ── grabItemFromChest / grabItemFromInventory prefixes ──────────

    /// <summary>
    /// Replaces <see cref="Chest.grabItemFromChest"/> for our enlarged
    /// chests.  Does the same inventory work (remove item, clear nulls)
    /// but skips the <c>ShowMenu()</c> call that would rebuild the entire
    /// <see cref="ItemGrabMenu"/> and cause a visible blink.
    /// </summary>
    private static bool GrabItemFromChest_Prefix(Chest __instance, Item item, Farmer who)
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu igm)
            return true;

        var foundChest = FindAutoGrabberChest(igm);
        bool hasKey = __instance.modData.ContainsKey(CapacityKey);
        bool refMatch = foundChest == __instance;

        if (!refMatch && !hasKey)
            return true;

        if (who.couldInventoryAcceptThisItem(item))
        {
            __instance.GetItemsForPlayer().Remove(item);
            __instance.clearNulls();
        }

        return false;
    }

    /// <summary>
    /// Replaces <see cref="Chest.grabItemFromInventory"/> for our
    /// enlarged chests.  Adds the item to the chest and handles the held-
    /// item / snap state on the existing menu without calling
    /// <c>ShowMenu()</c>.
    /// </summary>
    private static bool GrabItemFromInventory_Prefix(Chest __instance, Item item, Farmer who)
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu igm)
            return true;

        var foundChest = FindAutoGrabberChest(igm);
        bool hasKey = __instance.modData.ContainsKey(CapacityKey);
        bool refMatch = foundChest == __instance;

        if (!refMatch && !hasKey)
            return true;

        // Replicate vanilla logic without the ShowMenu() call.
        if (item.Stack == 0)
            item.Stack = 1;

        Item remainder = __instance.addItem(item);
        if (remainder == null)
            who.removeItemFromInventory(item);
        else
            remainder = who.addItemToInventory(remainder);

        __instance.clearNulls();

        // Preserve snapped component across the (non-)rebuild.
        int snappedId = igm.currentlySnappedComponent?.myID ?? -1;

        // Update heldItem on the existing menu (vanilla sets this on
        // the freshly-created menu returned by ShowMenu).
        igm.heldItem = remainder;

        if (snappedId != -1)
        {
            igm.currentlySnappedComponent = igm.getComponentWithID(snappedId);
            igm.snapCursorToCurrentSnappedComponent();
        }

        return false;
    }

    // ── Object.grabItemFromAutoGrabber prefix ──────────────────────

    /// <summary>
    /// Replaces <see cref="StardewValley.Object.grabItemFromAutoGrabber"/>
    /// for our enlarged auto-grabbers.  Does the same item-transfer work
    /// (remove item, clear nulls, update showNextIndex) but skips the
    /// <c>Game1.activeClickableMenu = new ItemGrabMenu(...)</c> call that
    /// rebuilds the entire menu and causes a visible blink.
    /// </summary>
    private static bool GrabItemFromAutoGrabber_Prefix(
        StardewValley.Object __instance, Item item, Farmer who)
    {
        if (__instance.heldObject.Value is not Chest chest
            || !chest.modData.ContainsKey(CapacityKey))
        {
            return true;
        }

        if (who.couldInventoryAcceptThisItem(item))
        {
            chest.Items.Remove(item);
            chest.clearNulls();
        }

        if (chest.isEmpty())
        {
            __instance.showNextIndex.Value = false;
        }

        return false;
    }

    // ── GetActualCapacity postfix ───────────────────────────────────

    private static void GetActualCapacity_Postfix(Chest __instance, ref int __result)
    {
        // During ItemGrabMenu construction, return the vanilla value so the
        // constructor uses its standard 36-slot layout path.  This avoids
        // the SetupBorderNeighbors crash for capacities whose column count
        // exceeds the player inventory size.
        if (_suppressCapacity)
            return;

        if (__instance.modData.TryGetValue(CapacityKey, out string val)
            && int.TryParse(val, out int cap)
            && cap > 0)
        {
            __result = cap;
        }
    }

    // ── Auto-grabber chest detection ────────────────────────────────

    /// <summary>Finds the auto-grabber chest backing the given menu, if any.</summary>
    public static Chest FindAutoGrabberChest(ItemGrabMenu menu)
    {
        if (menu.sourceItem is Chest srcChest
            && srcChest.modData.ContainsKey(CapacityKey))
        {
            return srcChest;
        }

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

    // ── Menu resize ─────────────────────────────────────────────────

    /// <summary>
    /// Replaces the menu's <see cref="ItemGrabMenu.ItemsToGrabMenu"/>
    /// with one sized for the given capacity, adjusts the menu height and
    /// player-inventory position, then lets the game reposition all
    /// buttons via its own <c>RepositionSideButtons</c> and
    /// <c>SetupBorderNeighbors</c> methods.
    /// </summary>
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

        // Build the replacement InventoryMenu at the same position.
        menu.ItemsToGrabMenu = new InventoryMenu(
            old.xPositionOnScreen,
            old.yPositionOnScreen,
            playerInventory: false,
            old.actualInventory,
            capacity: visibleCap,
            rows: visibleRows
        );

        // Replicate the ID fixup the constructor normally does (region
        // 53910 offset, fullyImmutable, downNeighborID sentinel).
        menu.ItemsToGrabMenu.populateClickableComponentList();
        for (int i = 0; i < menu.ItemsToGrabMenu.inventory.Count; i++)
        {
            var slot = menu.ItemsToGrabMenu.inventory[i];
            if (slot != null)
            {
                slot.myID += 53910;
                slot.upNeighborID += 53910;
                slot.rightNeighborID += 53910;
                slot.downNeighborID = -7777;
                slot.leftNeighborID += 53910;
                slot.fullyImmutable = true;
            }
        }

        // Expand the menu frame and push the player inventory down.
        menu.height += shift;
        menu.inventory.movePosition(0, shift);

        // Move okButton / trashCan down to match the taller menu.
        if (menu.okButton != null)
            menu.okButton.bounds.Y += shift;
        if (menu.trashCan != null)
            menu.trashCan.bounds.Y += shift;

        // Let the game position the side buttons (organize, fill-stacks,
        // color picker, etc.) relative to the new ItemsToGrabMenu.
        menu.RepositionSideButtons();

        // Rebuild keyboard / gamepad neighbour links so
        // SetupBorderNeighbors doesn't crash and navigation works.
        menu.SetupBorderNeighbors();
    }
}
