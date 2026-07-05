using System;
using MoreQuestsFramework.Api;

namespace MoreQuestsFramework.Config;

// Resolves a custom board's effective pin count from its authored PoolSize / PoolSizeMin /
// PoolSizeMax and the player's per-board GMCM override. Shared by the draw pipeline (which
// needs the count) and GMCM (which needs the slider bounds), so the clamp rules live in one
// place.
internal static class BoardPoolConfig
{
    // Config / slot lookup key. Matches CustomBoardSlots.KeyOf and CustomBoardRouting.KeyOf.
    public static string KeyOf(BoardDefinition board)
        => (board.OwnerUniqueId ?? "") + "/" + (board.Name ?? "");

    // Slider bounds shown in GMCM. The default value always sits inside the range, even if
    // the author set PoolSize outside their own min/max.
    public static (int min, int max, int def) Bounds(BoardDefinition board)
    {
        int def = Math.Max(0, board.PoolSize);
        int min = Math.Min(board.PoolSizeMin, def);
        int max = Math.Max(board.PoolSizeMax, def);
        if (max < min)
            max = min;
        return (min, max, def);
    }

    // The pin count actually drawn: the player's override (if any) clamped into the board's
    // bounds, else the authored default. Owner-managed boards skip the override dict entirely
    // and draw their authored PoolSize (which their own mod rewrites live), floored at 1.
    public static int Effective(BoardDefinition board, MoreQuestsFrameworkConfig config)
    {
        if (board.OwnerManagesPoolConfig)
            return Math.Max(1, board.PoolSize);

        var (min, max, def) = Bounds(board);
        int value = config.CustomBoardPoolSize.TryGetValue(KeyOf(board), out int stored) ? stored : def;
        return Math.Clamp(value, Math.Max(1, min), Math.Max(1, max));
    }
}
