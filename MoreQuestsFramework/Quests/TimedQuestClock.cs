using System;

namespace MoreQuestsFramework.Quests;

// Why the giver won't hand a timed package over right now. None means go ahead.
public enum TimedStartRefusal
{
    None,
    Festival,
    TooLate,
    InventoryFull,
    NoTarget
}

// Pure time math for in-game minute countdowns. Times use the game's HHMM clock
// format (600 = 6:00am, 1430 = 2:30pm, 2600 = 2:00am). Kept free of Game1 so the
// test project can cover it.
public static class TimedQuestClock
{
    // Latest moment a day can reach before the player passes out.
    public const int EndOfDay = 2600;

    public static int AddMinutes(int timeOfDay, int minutes)
    {
        int total = ToMinutes(timeOfDay) + minutes;
        return (total / 60) * 100 + total % 60;
    }

    // Positive when `to` is later in the same day than `from`.
    public static int MinutesBetween(int from, int to)
        => ToMinutes(to) - ToMinutes(from);

    // Rolls a limit in [min, max] snapped to 10-minute steps so the deadline always
    // lands on a clock tick. nextRandom(n) must return a value in [0, n), like
    // Game1.random.Next.
    public static int RollLimitMinutes(int minMinutes, int maxMinutes, Func<int, int> nextRandom)
    {
        int lo = Math.Max(10, minMinutes);
        int hi = Math.Max(lo, maxMinutes);
        lo = (lo + 9) / 10 * 10;
        hi = hi / 10 * 10;
        if (hi < lo)
            hi = lo;
        int steps = (hi - lo) / 10;
        return lo + 10 * nextRandom(steps + 1);
    }

    // How many whole in-game minutes have elapsed inside the current 10-minute clock
    // tick, derived from Game1.gameTimeInterval. Lets the HUD count down smoothly
    // instead of jumping 10 minutes every ~7 seconds. Time-speed mods that stretch
    // or freeze the interval are handled naturally.
    public static int MinutesIntoCurrentTick(int gameTimeInterval, int msPerTenMinutes)
    {
        if (msPerTenMinutes <= 0 || gameTimeInterval <= 0)
            return 0;
        int perMinute = msPerTenMinutes / 10;
        if (perMinute <= 0)
            return 0;
        return Math.Clamp(gameTimeInterval / perMinute, 0, 9);
    }

    public static int RemainingMinutes(int timeOfDay, int deadline, int gameTimeInterval, int msPerTenMinutes)
    {
        int remaining = MinutesBetween(timeOfDay, deadline)
            - MinutesIntoCurrentTick(gameTimeInterval, msPerTenMinutes);
        return Math.Max(0, remaining);
    }

    // The handoff gate. Refuses when the package couldn't plausibly be delivered:
    // festival days lock the town up, a late pickup can't fit the WORST case roll
    // before 2am (so the rolled limit is always honest), a full inventory can't
    // take the package, and no target means nobody reachable to deliver to.
    public static TimedStartRefusal CheckHandoffGate(
        int timeOfDay,
        int maxLimitMinutes,
        int latestStartTime,
        bool isFestivalToday,
        bool hasInventoryRoom,
        bool hasTarget)
    {
        if (isFestivalToday)
            return TimedStartRefusal.Festival;
        if (latestStartTime > 0 && timeOfDay > latestStartTime)
            return TimedStartRefusal.TooLate;
        if (AddMinutes(timeOfDay, Math.Max(10, maxLimitMinutes)) > EndOfDay)
            return TimedStartRefusal.TooLate;
        if (!hasInventoryRoom)
            return TimedStartRefusal.InventoryFull;
        if (!hasTarget)
            return TimedStartRefusal.NoTarget;
        return TimedStartRefusal.None;
    }

    private static int ToMinutes(int timeOfDay)
        => (timeOfDay / 100) * 60 + timeOfDay % 100;
}
