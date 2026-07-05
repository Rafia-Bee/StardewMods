using System;
using MoreQuestsFramework.Api;

namespace MoreQuestsFramework.Config;

// Resolves a board's effective notice-pin count from its authored NoticePoolSize / Min / Max
// and the player's per-board GMCM override. Parallel to BoardPoolConfig, but notices may go to
// zero (a player can hide them entirely), so this does not enforce a floor of 1.
internal static class NoticePoolConfig
{
    // Same config / slot lookup key as BoardPoolConfig.
    public static string KeyOf(BoardDefinition board)
        => (board.OwnerUniqueId ?? "") + "/" + (board.Name ?? "");

    public static (int min, int max, int def) Bounds(BoardDefinition board)
    {
        int def = Math.Max(0, board.NoticePoolSize);
        int min = Math.Max(0, Math.Min(board.NoticePoolSizeMin, def));
        int max = Math.Max(board.NoticePoolSizeMax, def);
        if (max < min)
            max = min;
        return (min, max, def);
    }

    public static int Effective(BoardDefinition board, MoreQuestsFrameworkConfig config)
    {
        if (board.OwnerManagesPoolConfig)
            return Math.Max(0, board.NoticePoolSize);

        var (min, max, def) = Bounds(board);
        int value = config.CustomBoardNoticePoolSize.TryGetValue(KeyOf(board), out int stored) ? stored : def;
        return Math.Clamp(value, min, max);
    }
}
