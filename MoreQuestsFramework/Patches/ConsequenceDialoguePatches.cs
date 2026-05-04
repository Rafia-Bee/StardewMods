using HarmonyLib;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

/// Harmony prefix on `NPC.checkAction` that pops a pending consequence line for the
/// target NPC (if any) before vanilla builds + draws the dialogue. Without this hook,
/// the 1 Hz `ConsequenceDialogueWatcher.Tick` fires *after* vanilla has already drawn
/// the normal dialogue, producing a visible flash where the normal line shows for a
/// frame and then gets replaced by the consequence line on the next tick.
///
/// Patch is fully gated per §8.1 — when no consequence lines are queued, the prefix
/// returns immediately after one int compare. The watcher stays as a backup for any
/// dialogue path that doesn't go through `checkAction` (e.g. NPC-initiated greetings
/// or scripted events that call `Game1.drawDialogue` directly), so a queued line is
/// never "stuck" if the patch misses an edge case.
internal static class ConsequenceDialoguePatches
{
    /// Live framework state set by `ModEntry.OnSaveLoaded` to point at the per-save
    /// `FrameworkState` instance. Null between saves; the prefix no-ops on null so
    /// patch overhead is one ref-equals + one int compare when no save is loaded.
    public static FrameworkState? ActiveState { get; set; }
    private static IMonitor? _monitor;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(ConsequenceDialoguePatches), nameof(CheckAction_Prefix)));
    }

    /// `__instance` is the NPC the player just clicked. Returns void so vanilla
    /// continues to its own `checkAction` body — we just slip a dialogue onto the top
    /// of `currentDialogue` first, which vanilla will then pop + draw normally.
    /// `who` is the farmer; `l` is the location. Neither is needed here but the
    /// prefix signature has to match Harmony's matching rules.
    public static void CheckAction_Prefix(NPC __instance, Farmer who)
    {
        if (ActiveState == null || __instance == null) return;
        var queue = ActiveState.PendingConsequenceLines;
        if (queue.Count == 0) return;

        // Skip during cutscenes / festival events. Without this guard, clicking an
        // NPC mid-cutscene (Krobus's vault scene, etc.) overwrites the scripted line
        // with the consequence line, breaking the cutscene's intended dialogue.
        if (Game1.eventUp || Game1.CurrentEvent != null) return;

        int today = Game1.Date?.TotalDays ?? 0;
        string npcName = __instance.Name;

        // Per-day clamp — at most one consequence line per NPC per day. Without this,
        // a player who skips ahead in time then talks to a chained-Tier3 NPC gets every
        // queued line back-to-back, which breaks immersion.
        if (ActiveState.LastConsequencePoppedDay.TryGetValue(npcName, out int lastDay) && lastDay >= today)
            return;

        // Tier 3 chains queue one entry per day with stepping `EarliestFireDay`. The
        // narrative is: line 1 on day 1, line 2 on day 2, line 3 on day 3. If the
        // player skips a day's chat (e.g. cheats forward, or just doesn't visit the
        // NPC), the eligible queue ends up with multiple entries for the same NPC.
        // Pop the entry with the GREATEST `EarliestFireDay` that's still <= today —
        // i.e. the most-recent narrative beat, not the oldest. Drop any earlier
        // eligible entries for the same NPC silently so the chain doesn't re-surface
        // out of order on subsequent chats.
        int bestIdx = -1;
        int bestDay = int.MinValue;
        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, npcName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.EarliestFireDay > today)
                continue;
            if (entry.EarliestFireDay > bestDay)
            {
                bestDay = entry.EarliestFireDay;
                bestIdx = i;
            }
        }
        if (bestIdx < 0)
            return;

        var chosen = queue[bestIdx];
        // Walk back-to-front so RemoveAt indices stay valid. Drop every earlier-day
        // eligible entry for this NPC — they're stale narrative beats the player
        // can't catch up on without time-travel.
        int dropped = 0;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (i == bestIdx) continue;
            var entry = queue[i];
            if (!string.Equals(entry.NpcName, npcName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.EarliestFireDay > today)
                continue;
            if (entry.EarliestFireDay >= bestDay)
                continue;
            queue.RemoveAt(i);
            dropped++;
            if (i < bestIdx) bestIdx--;
        }

        queue.RemoveAt(bestIdx);
        ActiveState.LastConsequencePoppedDay[npcName] = today;
        if (dropped > 0)
            _monitor?.Log(
                $"ConsequenceDialoguePatches: dropped {dropped} stale chain entries for {npcName} (player skipped earlier chat days).",
                LogLevel.Trace);
        FireOne(__instance, who, chosen);
    }

    private static void FireOne(NPC speaker, Farmer who, DialogueQueueEntry entry)
    {
        if (entry.FriendshipDelta != 0 && who != null)
        {
            int before = who.getFriendshipLevelForNPC(speaker.Name);
            who.changeFriendship(entry.FriendshipDelta, speaker);
            int after = who.getFriendshipLevelForNPC(speaker.Name);
            _monitor?.Log(
                $"ConsequenceDialoguePatches: {speaker.Name} friendship {before} -> {after} (delta {entry.FriendshipDelta:+#;-#;0}). If unchanged, check that 'no friendship decay' / friendship-clamp mods aren't intercepting changeFriendship.",
                LogLevel.Debug);
        }

        if (!string.IsNullOrEmpty(entry.Line))
        {
            // Portrait code goes at the END of the dialogue per SDV's parser
            // convention — placing it at the start only renders for one frame before
            // the page swap wipes it.
            string text = string.IsNullOrEmpty(entry.Portrait)
                ? entry.Line
                : entry.Line + entry.Portrait;
            speaker.CurrentDialogue.Push(new Dialogue(speaker, null, text));
        }

        _monitor?.Log(
            $"ConsequenceDialoguePatches: pushed line for {speaker.Name} via checkAction (friendship {entry.FriendshipDelta:+#;-#;0}, portrait '{entry.Portrait}').",
            LogLevel.Trace);
    }
}
