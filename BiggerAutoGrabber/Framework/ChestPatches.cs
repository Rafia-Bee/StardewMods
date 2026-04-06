using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    // ── Scroll state (only one menu active at a time) ───────────────
    private static ItemGrabMenu _scrollMenu;
    private static InventorySlice _activeSlice;
    private static ClickableTextureComponent _upArrow;
    private static ClickableTextureComponent _downArrow;

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

        // Scroll wheel handling for scrollable auto-grabber menus.
        harmony.Patch(
            original: AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.receiveScrollWheelAction)),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(ReceiveScrollWheelAction_Postfix))
        );

        // Draw scroll arrows on top of the menu.
        harmony.Patch(
            original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.draw), new[] { typeof(SpriteBatch) }),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(Draw_Postfix))
        );

        // Handle clicks on scroll arrows.
        harmony.Patch(
            original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.receiveLeftClick)),
            prefix: new HarmonyMethod(typeof(ChestPatches), nameof(ReceiveLeftClick_Prefix))
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
            && int.TryParse(capStr, out int cap))
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

        // Unwrap InventorySlice to get the real chest items for matching.
        if (sourceItems is InventorySlice slice)
            sourceItems = slice.Source;

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
        var old = menu.ItemsToGrabMenu;

        // Unwrap any existing InventorySlice to get the real item list.
        IList<Item> rawInventory = old.actualInventory;
        if (rawInventory is InventorySlice existingSlice)
            rawInventory = existingSlice.Source;

        // Use the larger of configured cap or actual item count so items
        // that exceed a reduced capacity are still reachable via scroll.
        int effectiveCap = Math.Max(cap, rawInventory.Count);
        int desiredRows = (int)Math.Ceiling(effectiveCap / (double)cols);
        int visibleRows = Math.Min(desiredRows, 6);
        int defaultRows = old.rows;
        int extraRows = visibleRows - defaultRows;
        int visibleCap = visibleRows * cols;
        bool needsScrolling = rawInventory.Count > visibleCap;

        if (extraRows <= 0 && !needsScrolling)
        {
            ClearScrollState();
            return;
        }

        int shift = extraRows > 0 ? extraRows * 64 : 0;

        InventorySlice slice = null;
        IList<Item> inventorySource = rawInventory;
        if (needsScrolling)
        {
            slice = new InventorySlice(rawInventory, visibleCap);
            inventorySource = slice;
        }

        // Build the replacement InventoryMenu at the same position.
        menu.ItemsToGrabMenu = new InventoryMenu(
            old.xPositionOnScreen,
            old.yPositionOnScreen,
            playerInventory: false,
            needsScrolling ? slice : inventorySource,
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
        if (shift > 0)
        {
            menu.height += shift;
            menu.inventory.movePosition(0, shift);

            // Move okButton / trashCan down to match the taller menu.
            if (menu.okButton != null)
                menu.okButton.bounds.Y += shift;
            if (menu.trashCan != null)
                menu.trashCan.bounds.Y += shift;
        }

        // Let the game position the side buttons (organize, fill-stacks,
        // color picker, etc.) relative to the new ItemsToGrabMenu.
        // Use reflection so the mod doesn't hard-reference methods that
        // may not exist on Android/mobile builds.
        typeof(ItemGrabMenu)
            .GetMethod("RepositionSideButtons", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(menu, null);

        // Rebuild keyboard / gamepad neighbour links so navigation works.
        typeof(ItemGrabMenu)
            .GetMethod("SetupBorderNeighbors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(menu, null);

        // Set up scroll state if the inventory needs scrolling.
        if (needsScrolling && slice != null)
        {
            _scrollMenu = menu;
            _activeSlice = slice;

            var grid = menu.ItemsToGrabMenu;
            int arrowX = grid.xPositionOnScreen + grid.width + 16;

            _upArrow = new ClickableTextureComponent(
                new Rectangle(arrowX, grid.yPositionOnScreen - 12, 44, 48),
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f);

            _downArrow = new ClickableTextureComponent(
                new Rectangle(arrowX, grid.yPositionOnScreen + grid.height - 48, 44, 48),
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f);
        }
        else
        {
            ClearScrollState();
        }
    }

    // ── Scroll state management ─────────────────────────────────────

    internal static void ClearScrollState()
    {
        _scrollMenu = null;
        _activeSlice = null;
        _upArrow = null;
        _downArrow = null;
    }

    // ── Scroll patches ──────────────────────────────────────────────

    private static void ReceiveScrollWheelAction_Postfix(IClickableMenu __instance, int direction)
    {
        if (_activeSlice == null || __instance != _scrollMenu)
            return;

        if (__instance is not ItemGrabMenu igm)
            return;

        var grid = igm.ItemsToGrabMenu;
        if (grid == null)
            return;

        int mx = Game1.getMouseX(true);
        int my = Game1.getMouseY(true);
        if (!grid.isWithinBounds(mx, my)
            && (_upArrow == null || !_upArrow.containsPoint(mx, my))
            && (_downArrow == null || !_downArrow.containsPoint(mx, my)))
            return;

        int oldRow = _activeSlice.ScrollRow;
        _activeSlice.ScrollRow += direction > 0 ? -1 : 1;

        if (_activeSlice.ScrollRow != oldRow)
            Game1.playSound("shiny4");
    }

    private static void Draw_Postfix(ItemGrabMenu __instance, SpriteBatch b)
    {
        if (_activeSlice == null || __instance != _scrollMenu)
            return;

        if (_activeSlice.CanScrollUp && _upArrow != null)
            _upArrow.draw(b);
        if (_activeSlice.CanScrollDown && _downArrow != null)
            _downArrow.draw(b);

        // Redraw mouse on top of arrows.
        __instance.drawMouse(b);
    }

    private static bool ReceiveLeftClick_Prefix(ItemGrabMenu __instance, int x, int y)
    {
        if (_activeSlice == null || __instance != _scrollMenu)
            return true;

        if (_upArrow != null && _upArrow.containsPoint(x, y) && _activeSlice.CanScrollUp)
        {
            _activeSlice.ScrollRow--;
            Game1.playSound("shiny4");
            return false;
        }

        if (_downArrow != null && _downArrow.containsPoint(x, y) && _activeSlice.CanScrollDown)
        {
            _activeSlice.ScrollRow++;
            Game1.playSound("shiny4");
            return false;
        }

        return true;
    }
}
