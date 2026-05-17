using System;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Characters;

namespace MoreQuestsFramework;

// Modded NPCs ship with dotted internal IDs ("MadDog.HashtagBearFam.Sondra",
// "NassiLove.AikawaAsakaiCP.Arumi", etc). Generators bake those into player-facing
// strings via i18n's {{npc}} token, and the raw ID leaks into the journal and mail.
// This helper resolves an internal name to the runtime DisplayName, and does a
// safe substring sweep on already-rendered text to swap any baked-in IDs.
public static class NpcDisplay
{
    // Falls back to the input when no display name is available.
    public static string Resolve(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName))
            return internalName ?? string.Empty;
        var npc = Game1.getCharacterFromName(internalName);
        string? display = npc?.displayName;
        if (string.IsNullOrWhiteSpace(display))
            return internalName;
        return display!;
    }

    // Used by QuestPoster to drop non-human NPC postings (Duck2NPC, LexiMonster, etc).
    // Rejects when: NPC is in the config IneligibleGivers list, is a child, CanSocialize
    // GSQ fails, or PerfectionScore is false. PerfectionScore catches the East Scarp
    // friendable animals (DuckNPC / Duck2NPC / HappySlime) that explicitly opt out, but
    // some packs (Leximonster, Sen from Lurking in the Dark) build their friendable
    // monsters with full perfection / slideshow entries, so the heuristic can't tell
    // them apart from a real human. IneligibleGivers is the manual override for those.
    // SocialTab can't be used because SVE flags real adults like Lance / Susan / Gunther
    // as HiddenUntilMet for narrative gating. ItemDeliveryQuests can't be used either
    // because RSV / East Scarp adults set it "FALSE" to opt out of vanilla's random
    // delivery rotation while still being valid hand-authored quest givers.
    public static bool IsBoardEligible(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName)) return false;
        var deny = ModEntry.Config?.IneligibleGivers;
        if (deny != null && deny.Contains(internalName, StringComparer.OrdinalIgnoreCase))
            return false;
        if (Game1.characterData == null) return true;
        if (!Game1.characterData.TryGetValue(internalName, out var data) || data == null)
            return true;
        if (data.Age == NpcAge.Child) return false;
        if (!StardewValley.GameStateQuery.CheckConditions(data.CanSocialize))
            return false;
        if (!data.PerfectionScore) return false;
        return true;
    }

    // Walks character data and replaces any internal name whose DisplayName differs.
    // Catches both dotted IDs ("MadDog.HashtagBearFam.Sondra" -> "Sondra") and plain
    // CamelCase ones ("MaddyPellegrinVMV" -> "Maddy"). Vanilla NPCs have internal name
    // == DisplayName in English so they're skipped by the equality check.
    public static string SubstituteIn(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        if (Game1.characterData == null) return text;
        foreach (var (internalName, _) in Game1.characterData)
        {
            if (string.IsNullOrEmpty(internalName))
                continue;
            if (text.IndexOf(internalName, StringComparison.Ordinal) < 0)
                continue;
            string display = Resolve(internalName);
            if (string.IsNullOrEmpty(display) || display == internalName)
                continue;
            text = text.Replace(internalName, display);
        }
        return text;
    }
}
