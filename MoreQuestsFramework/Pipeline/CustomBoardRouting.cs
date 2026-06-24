using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Pipeline;

// One built custom-board posting and every board it shows up on. A posting on its home
// board AND one or more catch-all boards is a single Slot mirrored across them, so the
// boards list can hold more than one entry.
internal sealed class CustomBoardDraw
{
    public QuestPosting Posting { get; }
    public List<BoardDefinition> Boards { get; } = new();

    public CustomBoardDraw(QuestPosting posting, BoardDefinition? homeBoard)
    {
        Posting = posting;
        if (homeBoard != null)
            Boards.Add(homeBoard);
    }
}

// Shared routing rules for custom-board quests, used by both the daily draw and the
// startup validation pass so the two never disagree on where a quest belongs.
internal static class CustomBoardRouting
{
    public enum HomeResolution
    {
        // The quest has a home board (explicit Trigger.CustomBoardId, or its owner's single board).
        Home,
        // No home board, but a catch-all board may still pick it up.
        Homeless,
        // Explicit Trigger.CustomBoardId names a board that isn't registered. Never appears.
        Drop
    }

    public static string KeyOf(BoardDefinition board)
        => (board.OwnerUniqueId ?? "") + "/" + (board.Name ?? "");

    public static (Dictionary<string, BoardDefinition> ByKey, Dictionary<string, List<BoardDefinition>> ByOwner)
        BuildBoardMaps(BoardRegistry boards)
    {
        var byKey = new Dictionary<string, BoardDefinition>(StringComparer.OrdinalIgnoreCase);
        var byOwner = new Dictionary<string, List<BoardDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var board in boards.All)
        {
            byKey[KeyOf(board)] = board;
            string owner = board.OwnerUniqueId ?? "";
            if (!byOwner.TryGetValue(owner, out var list))
                byOwner[owner] = list = new List<BoardDefinition>();
            list.Add(board);
        }
        return (byKey, byOwner);
    }

    public static HomeResolution ResolveHome(
        IQuestDefinition def,
        string? overrideBoard,
        Dictionary<string, BoardDefinition> byKey,
        Dictionary<string, List<BoardDefinition>> byOwner,
        out string homeKey)
    {
        homeKey = "";
        string owner = def.OwnerUniqueId ?? "";
        string? board = !string.IsNullOrWhiteSpace(overrideBoard) ? overrideBoard : def.Trigger?.CustomBoardId;

        if (!string.IsNullOrWhiteSpace(board))
        {
            homeKey = board!.Contains('/') ? board! : owner + "/" + board;
            return byKey.ContainsKey(homeKey) ? HomeResolution.Home : HomeResolution.Drop;
        }

        if (byOwner.TryGetValue(owner, out var owned) && owned.Count == 1)
        {
            homeKey = KeyOf(owned[0]);
            return HomeResolution.Home;
        }
        return HomeResolution.Homeless;
    }

    public static bool IsCatchAll(BoardDefinition board)
        => board.AllowedOwners != null && board.AllowedOwners.Count > 0;

    public static bool Catches(BoardDefinition board, string ownerUniqueId)
    {
        if (!IsCatchAll(board))
            return false;
        foreach (var entry in board.AllowedOwners!)
        {
            if (entry == "*")
                return true;
            if (string.Equals(entry, ownerUniqueId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // One loud, specific warning per unroutable custom-board quest, run once when boards
    // and quests are both loaded. The daily draw stays quiet so this isn't repeated nightly.
    public static void ValidateRouting(QuestRegistry registry, BoardRegistry boards, IMonitor monitor)
    {
        var (byKey, byOwner) = BuildBoardMaps(boards);
        var catchAlls = new List<BoardDefinition>();
        foreach (var board in boards.All)
            if (IsCatchAll(board))
                catchAlls.Add(board);

        foreach (var def in registry.All)
        {
            if (registry.EffectiveSource(def) != TriggerSource.CustomBoard)
                continue;

            var res = ResolveHome(def, registry.EffectiveBoard(def), byKey, byOwner, out string homeKey);
            if (res == HomeResolution.Home)
                continue;

            if (res == HomeResolution.Drop)
            {
                monitor.Log(
                    $"Custom-board quest '{def.Id}' (owner '{def.OwnerUniqueId}') sets Trigger.CustomBoardId to a board that isn't registered (resolved '{homeKey}'). It won't appear anywhere. Check the board id, or guard the quest with an Available HasMod condition if the board comes from another mod.",
                    LogLevel.Warn);
                continue;
            }

            string owner = def.OwnerUniqueId ?? "";
            bool caught = false;
            foreach (var board in catchAlls)
            {
                if (Catches(board, owner))
                {
                    caught = true;
                    break;
                }
            }
            if (caught)
                continue;

            int owned = byOwner.TryGetValue(def.OwnerUniqueId ?? "", out var list) ? list.Count : 0;
            monitor.Log(
                $"Custom-board quest '{def.Id}' has no Trigger.CustomBoardId and its owner '{def.OwnerUniqueId}' owns {owned} board(s) (the implicit default needs exactly 1). No catch-all board accepts it either, so it won't appear anywhere. Set Trigger.CustomBoardId to a board id.",
                LogLevel.Warn);
        }
    }
}
