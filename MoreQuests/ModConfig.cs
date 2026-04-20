namespace MoreQuests;

public sealed class ModConfig
{
    // Quest Board
    public int QuestsPerDay { get; set; } = 3;
    public int QuestDeadlineDays { get; set; } = 2;

    // Difficulty
    public bool DifficultyScaling { get; set; } = true;

    // Consequences
    public bool ConsequencesEnabled { get; set; } = true;

    // Modded content integration
    public bool IncludeModdedItems { get; set; } = true;
    public bool IncludeModdedNPCs { get; set; } = true;

    // NPC personality lists for consequence targeting
    public string[] EcologyMindedNPCs { get; set; } = { "Demetrius", "Linus" };
    public string[] BusinessRivalNPCs { get; set; } = { "Pierre", "Morris" };

    // Festival quests
    public bool FestivalQuestsEnabled { get; set; } = true;
    public int FestivalQuestLeadDays { get; set; } = 3;

    // Animal quests
    public bool AnimalQuestsEnabled { get; set; } = true;
}
