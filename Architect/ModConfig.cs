namespace Architect;

// All tunables. Defaults are picked so a careful player who buys mid-priced furniture
// stays under budget and the leftover that returns to the NPC is small.
public sealed class ModConfig
{
    // Relative weight of the Architect quest in the daily-board pool. 0 disables it.
    public int QuestWeight { get; set; } = 12;

    // Days the player has to finish once they accept.
    public int DeadlineDays { get; set; } = 7;

    // Days before the same NPC can post another redecoration quest.
    public int CooldownDays { get; set; } = 14;

    // How many distinct furniture objectives a quest asks for.
    public int MinObjectives { get; set; } = 2;
    public int MaxObjectives { get; set; } = 3;

    // How many pieces each objective asks for.
    public int MinPerObjective { get; set; } = 1;
    public int MaxPerObjective { get; set; } = 2;

    // Budget = (sum of reference prices x counts) x generosity, rounded to the nearest 100.
    // Above 1.0 leaves the player a little slack so staying under budget is achievable.
    public double BudgetGenerosity { get; set; } = 1.3;

    // Reference shop prices per category, used only to size the budget.
    public int ReferenceLightPrice { get; set; } = 1000;
    public int ReferenceRugPrice { get; set; } = 1000;
    public int ReferenceChairPrice { get; set; } = 1000;
    public int ReferenceTablePrice { get; set; } = 1500;

    // How many reward items the giver hands over (loved first, then liked).
    public int RewardItemCount { get; set; } = 5;
}
