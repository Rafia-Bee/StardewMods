namespace MoreQuestsFramework.Consequences;

public interface IConsequenceHandler
{
    void Apply(ConsequenceContext ctx);
}
