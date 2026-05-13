namespace MoreQuestsFramework.Triggers;

/// What event causes a quest definition to attempt to fire today. Phase 6 covers
/// every source from plan.md §5.4 except `SpecialOrder` (Phase 8) and `CustomBoard`
/// (Phase 8). The trigger source is independent of the delivery channel
/// (`PostingKind`), a `DateLocked` quest can be delivered by mail, by NPC dialogue,
/// or written into the special-orders board, and the JSON `Delivery` field on the
/// trigger picks the channel.
public enum TriggerSource
{
    /// Weighted draw from the daily-board pool. The default for backwards compatibility.
    DailyBoard,

    /// Legacy "post via mail when conditions allow, then respect cooldown" trigger.
    /// Pre-Phase-6 mod-mail quests use this. New mods should prefer the explicit
    /// `Periodic` source for fixed-cadence mail quests.
    Mail,

    /// Fires every N in-game days. Tracks the absolute `Game1.Date.TotalDays` of the
    /// last fire in framework save state.
    Periodic,

    /// Fires on a specific in-game date. Optional `RepeatYearly` re-arms the trigger
    /// at the start of each year so seasonal quests post every year.
    DateLocked,

    /// Fires every day inside a closed date range, e.g. `winter 12` to `winter 13`
    /// for a two-day Festival of Ice prep window.
    DateRange,

    /// Fires once per save when the `When` predicate first evaluates true.
    OneShot,

    /// Fires the day a farm building of the named type is added (with optional
    /// `DayDelay`). Diffs the previous-day building snapshot persisted in save state.
    BuildingBuilt,

    /// Fires the day a mail flag enters `Game1.player.mailReceived` (with optional
    /// `DayDelay`). Diffs the previous-day mail snapshot persisted in save state.
    MailReceived,

    /// Fires when tomorrow's weather matches. Useful for "rainy-day" quest mail that
    /// arrives the night before so the player has a chance to prep.
    WeatherForecast,

    /// Defers delivery until the player next speaks with the named NPC. The framework
    /// queues the posting and a watcher pushes it into the journal at that moment.
    NpcDialogue,

    /// Phase 8: writes the quest into `Data/SpecialOrders` for the duration window.
    SpecialOrder,

    /// Phase 8: weighted draw from a custom board's pool.
    CustomBoard
}
