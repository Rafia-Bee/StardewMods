using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace BulkDerbyRewards.Framework;

/// <summary>Harmony patches that intercept the Trout Derby booth exchange.</summary>
internal static class DerbyPatches
{
    /// <summary>Dialogue key used by the mod's own bulk-exchange confirmation.</summary>
    private const string BulkExchangeDialogueKey = "BulkDerbyRewards_Exchange";

    /// <summary>Items waiting to be collected from the reward menu.</summary>
    private static List<Item> _pendingRewards;

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
            prefix: new HarmonyMethod(typeof(DerbyPatches), nameof(AnswerDialogueAction_Prefix))
        );
    }

    // ── Prefix ────────────────────────────────────────────────────────────

    private static bool AnswerDialogueAction_Prefix(
        GameLocation __instance,
        string questionAndAnswer,
        string[] questionParams,
        ref bool __result)
    {
        if (!ModEntry.Config.Enabled)
            return true;

        // Log all dialogue actions during derby days for debugging / future-proofing.
        if (IsTroutDerbyDay())
        {
            ModEntry.ModMonitor.Log(
                $"[BulkDerby] answerDialogueAction: '{questionAndAnswer}'",
                LogLevel.Trace);
        }

        // ── Handle our own bulk-exchange confirmation ──
        if (questionAndAnswer == $"{BulkExchangeDialogueKey}_Yes")
        {
            PerformBulkExchange();
            __result = true;
            return false;
        }

        // ── Intercept the vanilla Trout Derby exchange ──
        if (!IsTroutDerbyExchange(questionAndAnswer))
            return true;

        int tagCount = TroutDerbyPrizes.CountGoldenTags();
        if (tagCount == 0)
            return true; // Let vanilla show "no tags" message.

        bool shouldBulk = tagCount > 1 || ModEntry.Config.AlwaysBulk;
        if (!shouldBulk)
            return true; // Single tag, vanilla behaviour.

        // Show a confirmation dialogue before bulk-exchanging.
        string message = ModEntry.ModHelper.Translation.Get("dialogue.bulk_exchange",
            new { count = tagCount });

        __instance.createQuestionDialogue(
            message,
            new[]
            {
                new Response("Yes", ModEntry.ModHelper.Translation.Get("dialogue.yes")),
                new Response("No", ModEntry.ModHelper.Translation.Get("dialogue.no"))
            },
            BulkExchangeDialogueKey);

        __result = true;
        return false; // Skip the vanilla single-tag exchange.
    }

    // ── Core logic ────────────────────────────────────────────────────────

    private static void PerformBulkExchange()
    {
        int tagCount = TroutDerbyPrizes.CountGoldenTags();
        if (tagCount == 0)
            return;

        List<Item> prizes = TroutDerbyPrizes.GeneratePrizes(tagCount);
        TroutDerbyPrizes.RemoveAllGoldenTags();

        ModEntry.ModMonitor.Log(
            $"[BulkDerby] Exchanged {tagCount} Golden Tag(s) for {prizes.Count} prize(s).",
            LogLevel.Info);

        ShowRewardMenu(prizes);
    }

    /// <summary>Open a chest-style menu so the player can collect prizes at their own pace.</summary>
    private static void ShowRewardMenu(List<Item> prizes)
    {
        _pendingRewards = new List<Item>(prizes);

        // Build an inventory list for the grab menu (pad to 36 slots like a chest).
        var inventory = new List<Item>(prizes);
        while (inventory.Count < 36)
            inventory.Add(null);

        var menu = new ItemGrabMenu(
            inventory: inventory,
            reverseGrab: false,
            showReceivingMenu: true,
            highlightFunction: InventoryMenu.highlightAllItems,
            behaviorOnItemSelectFunction: null,
            message: null,
            behaviorOnItemGrab: null,
            snapToBottom: false,
            canBeExitedWithKey: true,
            playRightClickSound: true,
            allowRightClick: true,
            showOrganizeButton: false,
            source: ItemGrabMenu.source_chest,
            sourceItem: null,
            whichSpecialButton: -1,
            context: null);

        // When the player closes the menu, drop uncollected items on the ground.
        menu.exitFunction = () => OnRewardMenuClosed(inventory);

        Game1.activeClickableMenu = menu;
    }

    /// <summary>Drop any remaining items when the reward menu is closed.</summary>
    private static void OnRewardMenuClosed(List<Item> inventory)
    {
        var leftover = inventory.Where(i => i != null).ToList();
        if (leftover.Count == 0)
            return;

        foreach (Item item in leftover)
        {
            Game1.createItemDebris(
                item,
                Game1.player.getStandingPosition(),
                Game1.player.FacingDirection,
                Game1.currentLocation);
        }

        Game1.addHUDMessage(new HUDMessage(
            ModEntry.ModHelper.Translation.Get("hud.prizes_dropped"),
            HUDMessage.error_type));
    }

    // ── Detection helpers ─────────────────────────────────────────────────

    /// <summary>Check whether the current day is a Trout Derby day (Summer 20-21).</summary>
    private static bool IsTroutDerbyDay()
    {
        return Game1.season == Season.Summer
            && (Game1.dayOfMonth == 20 || Game1.dayOfMonth == 21);
    }

    /// <summary>
    /// Determine whether a dialogue-answer key represents accepting the
    /// Trout Derby exchange.  We match broadly because the exact key
    /// depends on the game version.
    /// </summary>
    private static bool IsTroutDerbyExchange(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        // Must reference the Trout Derby.
        if (key.IndexOf("TroutDerby", StringComparison.OrdinalIgnoreCase) < 0
            && key.IndexOf("troutDerby", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        // Must be a positive / acceptance response, not a refusal or info request.
        if (key.EndsWith("_No", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("_Leave", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("_Info", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("_Explain", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
