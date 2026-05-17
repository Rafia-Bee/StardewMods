namespace MoreQuestsFramework.Triggers;

// Passed to handlers registered via IMoreQuestsModApi.RegisterCustomTrigger. The
// handler returns true to fire the trigger today. The framework still applies the
// definition's CooldownDays before the handler is called, so the handler only needs
// to answer "is the gating event happening today?".
public sealed class CustomTriggerContext
{
    public string DefinitionId { get; }
    public string OwnerUniqueId { get; }
    public int TodayTotalDays { get; }
    // -1 when the definition has never fired on this save.
    public int LastFiredDay { get; }

    internal CustomTriggerContext(string definitionId, string ownerUniqueId, int today, int lastFiredDay)
    {
        DefinitionId = definitionId;
        OwnerUniqueId = ownerUniqueId;
        TodayTotalDays = today;
        LastFiredDay = lastFiredDay;
    }
}
