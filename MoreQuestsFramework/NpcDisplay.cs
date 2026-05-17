using System;
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

    // Mirrors vanilla's filter in ItemDeliveryQuest.GetValidTargetList: an NPC is
    // eligible to post a daily-board / special-order quest only if CanSocialize is
    // true, ItemDeliveryQuests passes (or HomeRegion is "Town" when the field is
    // null), and the NPC is not a child. Used by QuestPoster to redirect non-human
    // NPC postings (Duck2NPC, LexiMonster, etc.) from boards to mail.
    public static bool IsBoardEligible(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName)) return false;
        if (Game1.characterData == null) return true;
        if (!Game1.characterData.TryGetValue(internalName, out var data) || data == null)
            return true;
        if (data.Age == NpcAge.Child) return false;
        if (!StardewValley.GameStateQuery.CheckConditions(data.CanSocialize))
            return false;
        bool itemDeliveryOk = data.ItemDeliveryQuests != null
            ? StardewValley.GameStateQuery.CheckConditions(data.ItemDeliveryQuests)
            : data.HomeRegion == "Town";
        return itemDeliveryOk;
    }

    // Only sweeps when the text actually contains a dotted token, so the no-op
    // path is one IndexOf. Modded IDs without a dot don't need substitution since
    // their internal name typically equals their DisplayName already.
    public static string SubstituteIn(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        if (text.IndexOf('.') < 0) return text;
        if (Game1.characterData == null) return text;
        foreach (var (internalName, _) in Game1.characterData)
        {
            if (string.IsNullOrEmpty(internalName) || internalName.IndexOf('.') < 0)
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
