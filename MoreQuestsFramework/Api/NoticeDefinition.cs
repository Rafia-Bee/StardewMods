using System.Collections.Generic;
using MoreQuestsFramework.Content;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Api;

// Authoring spec for a bulletin notice: a non-quest pin on a custom board that opens a
// read-only text popup instead of a quest-accept popup. Registered via
// IMoreQuestsModApi.RegisterNotice or auto-loaded from the Notices content asset
// (Mods/RafiaBee.MoreQuestsFramework/Notices, a dict like Quests/Boards).
//
// A notice rides the same board, the same category styling (color / corner icon / pad+pin
// art), and the same Available condition keys as a quest. It just shows text instead of
// asking the player to take on a task.
public sealed class NoticeDef
{
    // Set by the loader to the dict key. Doubles as the persistence key, so don't rename a
    // notice after release if you use Once / a cooldown.
    public string? Name { get; set; }

    // Mod that owns this notice. When unset the loader infers it from the dict key
    // (everything before the first '_', if that prefix contains '.'). CP authors set
    // "Owner": "{{ModId}}"; DLL mods set it in their IAssetRequested.Edit handler.
    public string? Owner { get; set; }

    // The custom board this notice posts to, by the board's unique id. Same routing as a
    // quest's Trigger.CustomBoardId: a bare name resolves under this notice's owner,
    // "Owner.UniqueID/Name" targets another mod's board, and an omitted value falls back to
    // the owner's single board (dropped with a log line if the owner has 0 or 2+ boards).
    public string? CustomBoardId { get; set; }

    // Drives the pad/pin color, the corner icon, the per-category pad/pin art, and zone
    // routing, exactly like a quest's category. Defaults to "Social" when omitted.
    public string? Category { get; set; }

    // Draw weight when more notices are eligible than there are notice slots. Higher = more
    // likely to win a slot. 0 = never shown. Defaults to 1 so a notice posts without tuning.
    public int Weight { get; set; } = 1;

    // Gates whether the notice is eligible today. Same dictionary and the same keys as a
    // quest's Available block (Season, Date, DaysOfWeek, Random, MailReceived, Gsq, custom
    // conditions, the "not:" prefix, "|" alternatives, all of it).
    [JsonConverter(typeof(ScalarStringDictionaryConverter))]
    public Dictionary<string, string>? Available { get; set; }

    // Days the same notice waits before it can be drawn again after it last appeared.
    // 0 = no cooldown (a recurring notice that can re-roll every day). Use this to rotate
    // news so the same announcement doesn't sit up forever.
    public int CooldownDays { get; set; }

    // Named cooldown bucket resolved against the CooldownTiers asset, like a quest's
    // Trigger.CooldownTier. Null/unknown falls back to CooldownDays.
    public string? CooldownTier { get; set; }

    // One-shot. When true, the notice is shown on the first day it's drawn and then never
    // again on this save (the seen flag is saved). Leave false for a recurring notice.
    public bool Once { get; set; }

    // Pin the notice up for this many days once it's drawn. While pinned it shows every day
    // without being re-rolled, so it can't get bumped by other notices. 0 (default) means it
    // isn't pinned, so it's a normal daily draw. Ignored when Lifetime is set.
    public int PinDays { get; set; }

    // A named pin window instead of a fixed day count. "Week" pins the notice until the end of
    // the current week (the calendar's 7-day block: days 1 to 7, 8 to 14, and so on), so one
    // entry holds for the rest of that week without re-rolling. Takes precedence over PinDays.
    public string? Lifetime { get; set; }

    // Player-facing heading. Shown on the pin's hover tooltip and as the popup heading.
    // Write it in first person, like the notice author is speaking. CP resolves
    // {{i18n:key}} tokens before the text reaches the framework.
    public string? Title { get; set; }

    // Player-facing body. The text shown in the read-only popup. First person. Supports the
    // same {{i18n:key}} CP token resolution as Title.
    public string? Body { get; set; }

    // Optional NPC whose portrait fills the corner icon when the category (or this notice's
    // Icon) is "Portrait". Empty = no portrait.
    public string? Giver { get; set; }

    // Optional per-notice corner icon override, beating the category's icon. Same values as
    // CategoryDefinition.Icon: "Portrait", "None", or an asset name. Size and position still
    // come from the category.
    public string? Icon { get; set; }

    // Set by the registry to the owning mod's UniqueID. Mirrors BoardDefinition.OwnerUniqueId.
    public string OwnerUniqueId { get; set; } = "";
}
