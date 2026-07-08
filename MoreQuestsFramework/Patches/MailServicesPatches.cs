using HarmonyLib;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MoreQuestsFramework.Patches;

// Mail Services Mod lets the player turn in delivery-quest items at the mailbox, but its
// shortcut pays a hardcoded 255 friendship (150 for daily quests) on top of whatever the
// quest itself pays, and it skips our quality, alternative-item and partial-stack rules.
// This prefix outranks MSM's own GameLocation.mailbox prefixes (Priority.High vs. its
// default), handles our delivery quests through TryAccept so the quest's declared rewards
// are the only payout, and returns false so MSM never touches them. It also serves
// AdventureQuest deliver steps, which MSM can't see at all. Vanilla quests are left for
// MSM to handle as usual. Only patched when MSM is installed.
internal static class MailServicesPatches
{
    private const string MsmId = "Digus.MailServicesMod";

    internal enum MailboxOutcome { PassThrough, Deliver, NoMoney, Reject }

    internal readonly record struct MailQuestView(bool IsFrameworkQuest, bool Accepts, bool PrimaryIdMatches);

    private static IMonitor? _monitor;
    private static FieldInfo? _configField;
    private static FieldInfo? _disableQuestService;
    private static FieldInfo? _questServiceFee;
    private static FieldInfo? _showDialogOnItemDelivery;
    private static bool _reflectionFailed;

    public static void Apply(Harmony harmony, IModRegistry modRegistry, IMonitor monitor)
    {
        if (!modRegistry.IsLoaded(MsmId))
            return;
        _monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.mailbox)),
            prefix: new HarmonyMethod(typeof(MailServicesPatches), nameof(Mailbox_Prefix)) { priority = Priority.High });
    }

    public static bool Mailbox_Prefix()
    {
        try
        {
            return HandleMailbox();
        }
        catch (Exception ex)
        {
            _monitor?.Log($"Mail Services delivery shim failed, letting the mailbox run normally. {ex}", LogLevel.Warn);
            return true;
        }
    }

    private static bool HandleMailbox()
    {
        Farmer player = Game1.player;
        if (player == null || player.mailbox.Count > 0)
            return true;
        Item? held = player.ActiveObject;
        if (held == null)
            return true;
        if (!TryReadMsmConfig(out bool disabled, out int fee, out bool showDialog) || disabled)
            return true;

        // Mirror MSM's scan order so "which quest gets the item" doesn't change,
        // only who processes it. AdventureQuests are invisible to MSM (not
        // ItemDeliveryQuests), so including them only adds mailbox support for
        // their deliver steps, it can't collide with MSM's own matching.
        var quests = new List<Quest>();
        var views = new List<MailQuestView>();
        var targets = new List<NPC?>();
        foreach (Quest quest in player.questLog)
        {
            if (quest is AdventureQuest adventure && !adventure.completed.Value)
            {
                bool accepts = adventure.TryResolveMailDeliveryTarget(held, out NPC? stepNpc);
                quests.Add(adventure);
                views.Add(new MailQuestView(IsFrameworkQuest: true, Accepts: accepts, PrimaryIdMatches: false));
                targets.Add(stepNpc);
                continue;
            }
            if (quest is not ItemDeliveryQuest idq || idq.completed.Value)
                continue;
            bool primaryMatch = held.QualifiedItemId == idq.ItemId.Value;
            if (idq is MoreQuestsItemDeliveryQuest mq)
            {
                NPC? npc = Game1.getCharacterFromName(mq.target.Value);
                bool accepts = npc != null && mq.TryAccept(npc, held, probe: true, showNpcDialogue: showDialog);
                quests.Add(mq);
                views.Add(new MailQuestView(IsFrameworkQuest: true, Accepts: accepts, PrimaryIdMatches: primaryMatch));
                targets.Add(npc);
            }
            else
            {
                quests.Add(idq);
                views.Add(new MailQuestView(IsFrameworkQuest: false, Accepts: false, PrimaryIdMatches: primaryMatch));
                targets.Add(null);
            }
        }

        (MailboxOutcome outcome, int index) = Decide(views, player.Money, fee);
        switch (outcome)
        {
            case MailboxOutcome.Deliver:
            {
                Quest quest = quests[index];
                NPC npc = targets[index]!;
                int goldBefore = player.Money;
                int friendshipBefore = player.tryGetFriendshipLevelForNPC(npc.Name) ?? 0;
                ShopMenu.chargePlayer(player, 0, fee);
                if (quest is AdventureQuest adventure)
                    adventure.TryAcceptByMail(npc, held, showNpcDialogue: showDialog);
                else
                    ((MoreQuestsItemDeliveryQuest)quest).TryAccept(npc, held, probe: false, showNpcDialogue: showDialog);
                ModEntry.LogDebug(() => $"Mailbox delivery for '{quest.questTitle}': fee {fee}g, gold {goldBefore}g -> {player.Money}g, "
                    + $"friendship with {npc.Name} {friendshipBefore} -> {player.tryGetFriendshipLevelForNPC(npc.Name) ?? 0}, "
                    + $"completed: {quest.completed.Value}.");
                return false;
            }
            case MailboxOutcome.NoMoney:
                ModEntry.LogDebug($"Mailbox delivery refused: fee is {fee}g, player has {player.Money}g.");
                Game1.drawObjectDialogue(ModEntry.Translation?.Get("quest.itemDelivery.mail.noMoney").ToString() ?? "Not enough money for the delivery fee.");
                return false;
            case MailboxOutcome.Reject:
                ModEntry.LogDebug($"Mailbox delivery blocked: {held.QualifiedItemId} matches a framework quest's requested item, but the quest refused it (quality too low, locked to a different alternative, or NPC missing). Mail Services Mod was stopped from completing it.");
                Game1.drawObjectDialogue(ModEntry.Translation?.Get("quest.itemDelivery.mail.rejected").ToString() ?? "The package came back. It isn't quite what they asked for.");
                return false;
            default:
                return true;
        }
    }

    // Pure decision over the quest-log scan, split out so it can be unit tested.
    // MSM completes the first quest whose primary item id matches the held item, so once
    // one of our quests matches but can't accept (quality too low, locked to a different
    // alternative, missing NPC), MSM must be blocked entirely; letting it run would
    // complete that quest while ignoring the rule that made us refuse it.
    internal static (MailboxOutcome Outcome, int QuestIndex) Decide(IReadOnlyList<MailQuestView> quests, int playerMoney, int fee)
    {
        bool blocked = false;
        for (int i = 0; i < quests.Count; i++)
        {
            MailQuestView q = quests[i];
            if (q.IsFrameworkQuest)
            {
                if (q.Accepts)
                    return (playerMoney >= fee ? MailboxOutcome.Deliver : MailboxOutcome.NoMoney, i);
                if (q.PrimaryIdMatches)
                    blocked = true;
            }
            else if (q.PrimaryIdMatches)
            {
                return (blocked ? MailboxOutcome.Reject : MailboxOutcome.PassThrough, i);
            }
        }
        return (blocked ? MailboxOutcome.Reject : MailboxOutcome.PassThrough, -1);
    }

    private static bool TryReadMsmConfig(out bool disabled, out int fee, out bool showDialog)
    {
        disabled = false;
        fee = 0;
        showDialog = false;
        if (_reflectionFailed)
            return false;

        if (_configField == null)
        {
            Type? dataLoader = AccessTools.TypeByName("MailServicesMod.DataLoader");
            Type? configType = AccessTools.TypeByName("MailServicesMod.ModConfig");
            _configField = dataLoader == null ? null : AccessTools.Field(dataLoader, "ModConfig");
            _disableQuestService = configType == null ? null : AccessTools.Field(configType, "DisableQuestService");
            _questServiceFee = configType == null ? null : AccessTools.Field(configType, "QuestServiceFee");
            _showDialogOnItemDelivery = configType == null ? null : AccessTools.Field(configType, "ShowDialogOnItemDelivery");
            if (_configField == null || _disableQuestService == null || _questServiceFee == null || _showDialogOnItemDelivery == null)
            {
                _reflectionFailed = true;
                _monitor?.Log("Couldn't read Mail Services Mod's config, so its mailbox deliveries will use its own flow (hardcoded friendship) for framework quests.", LogLevel.Warn);
                return false;
            }
        }

        object? config = _configField.GetValue(null);
        if (config == null)
            return false;
        disabled = (bool)_disableQuestService!.GetValue(config)!;
        fee = (int)_questServiceFee!.GetValue(config)!;
        showDialog = (bool)_showDialogOnItemDelivery!.GetValue(config)!;
        return true;
    }
}
