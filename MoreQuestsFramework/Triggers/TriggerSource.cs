namespace MoreQuestsFramework.Triggers;

// Trigger source is independent of the delivery channel (PostingKind). The JSON
// Delivery field picks the channel.
public enum TriggerSource
{
    DailyBoard,
    Mail,
    Periodic,
    DateLocked,
    DateRange,
    OneShot,
    BuildingBuilt,
    MailReceived,
    WeatherForecast,
    NpcDialogue,
    SpecialOrder,
    CustomBoard,
    Custom
}
