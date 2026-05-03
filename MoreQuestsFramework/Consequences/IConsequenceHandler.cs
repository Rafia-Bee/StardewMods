namespace MoreQuestsFramework.Consequences;

/// Pluggable per-tier consequence behaviour. The engine picks one handler per
/// `ConsequenceTier` and forwards `Apply` to it. Built-ins cover Tier 0/1/2/3 + Special;
/// authors can call `IMoreQuestsModApi.RegisterConsequenceTier` to override one or
/// register a new tier name (the enum stays five-valued — extension lives in the
/// dispatch table, not the type).
///
/// Handlers receive a `ConsequenceContext` carrying the spec, the resolved NPC list,
/// the framework config (for default friendship deltas), the gift-tastes scanner, and
/// the per-save dialogue queue they should append entries to.
public interface IConsequenceHandler
{
    void Apply(ConsequenceContext ctx);
}
