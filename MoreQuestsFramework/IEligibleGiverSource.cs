using System.Collections.Generic;

namespace MoreQuestsFramework;

// Optional hook a quest definition can implement so the mq_givers console command can
// list which NPCs are currently eligible to be its giver. Reads live game state, the same
// way IsAvailable does. Purely a debug/inspection aid; the framework never calls it during
// normal posting. Return internal NPC names (e.g. "Haley"); an empty list means nobody
// qualifies right now.
public interface IEligibleGiverSource
{
    IReadOnlyList<string> GetEligibleGivers();
}
