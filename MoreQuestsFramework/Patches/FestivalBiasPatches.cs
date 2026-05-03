using System;
using HarmonyLib;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

/// Harmony postfixes on `Event.governorTaste` (Luau) and `Event.judgeGrange` (Fair).
/// Both fast-path out when no `ActiveFestivalBias` is recorded for that festival,
/// so the patches stay near-free for non-quest sessions per §8.1's gating rules.
///
/// Luau path: vanilla writes the next-step event command as `"switchEvent governorReactionN"`
/// where `N` is 0..6 (6 = Mayor's Shorts gag, untouchable). The postfix peeks the queued
/// command, parses the trailing digit, bumps it by the bias magnitude, clamps to 5, and
/// writes the new command back. We never overwrite a 6.
///
/// Fair path: vanilla writes `grangeScore = num` at the end of `judgeGrange`. The postfix
/// reads the field, adds the bias magnitude, and writes it back. The "Mayor's Shorts"
/// disqualification (-666) is preserved since we no-op when the score is negative.
internal static class FestivalBiasPatches
{
    private static IMonitor? _monitor;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        _monitor = monitor;

        var luauTaste = AccessTools.Method(typeof(Event), "governorTaste");
        if (luauTaste != null)
        {
            harmony.Patch(
                original: luauTaste,
                postfix: new HarmonyMethod(typeof(FestivalBiasPatches), nameof(GovernorTaste_Postfix)));
        }
        else
        {
            monitor.Log("FestivalBiasPatches: Event.governorTaste not found; Luau bias inactive.", LogLevel.Warn);
        }

        var judgeGrange = AccessTools.Method(typeof(Event), "judgeGrange");
        if (judgeGrange != null)
        {
            harmony.Patch(
                original: judgeGrange,
                postfix: new HarmonyMethod(typeof(FestivalBiasPatches), nameof(JudgeGrange_Postfix)));
        }
        else
        {
            monitor.Log("FestivalBiasPatches: Event.judgeGrange not found; Fair bias inactive.", LogLevel.Warn);
        }
    }

    public static void GovernorTaste_Postfix(Event __instance)
    {
        try
        {
            var writer = FestivalBiasWriter.Active;
            if (writer == null)
                return;
            int bump = writer.PeekMagnitude(FestivalKind.Luau);
            if (bump <= 0)
                return;

            // `eventCommands` and `CurrentCommand` are both private; AccessTools resolves
            // them by name without forcing us to recompile against an internal API.
            var commands = AccessTools.Field(typeof(Event), "eventCommands")?.GetValue(__instance) as System.Collections.Generic.IList<string>;
            int currentCmd = (int?)AccessTools.Property(typeof(Event), "CurrentCommand")?.GetValue(__instance) ?? -1;
            if (commands == null || currentCmd < 0 || currentCmd + 1 >= commands.Count)
                return;

            string queued = commands[currentCmd + 1] ?? string.Empty;
            const string prefix = "switchEvent governorReaction";
            if (!queued.StartsWith(prefix, StringComparison.Ordinal))
                return;
            string tail = queued.Substring(prefix.Length);
            if (!int.TryParse(tail, out int tier))
                return;

            // Tier 6 is the Mayor's Shorts gag; never overwrite it. Otherwise clamp to 5
            // ("loved it") so the bias can't push past the highest legitimate reaction.
            if (tier == 6)
                return;
            int boosted = Math.Min(5, tier + bump);
            if (boosted == tier)
                return;
            commands[currentCmd + 1] = prefix + boosted;
            writer.Consume(FestivalKind.Luau);
            _monitor?.Log($"FestivalBias (Luau): governor reaction tier {tier} → {boosted}.", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"FestivalBias Luau patch error: {ex.Message}", LogLevel.Warn);
        }
    }

    public static void JudgeGrange_Postfix(Event __instance)
    {
        try
        {
            var writer = FestivalBiasWriter.Active;
            if (writer == null)
                return;
            int bonus = writer.PeekMagnitude(FestivalKind.Fair);
            if (bonus <= 0)
                return;

            var grangeField = AccessTools.Field(typeof(Event), "grangeScore");
            if (grangeField == null)
                return;
            int score = (int?)grangeField.GetValue(__instance) ?? 0;
            // Vanilla sets `-666` when the player's display violates the Mayor's Shorts
            // disqualification; respect that and don't lift them out of the penalty.
            if (score < 0)
                return;
            int boosted = score + bonus;
            grangeField.SetValue(__instance, boosted);
            writer.Consume(FestivalKind.Fair);
            _monitor?.Log($"FestivalBias (Fair): grange score {score} → {boosted}.", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"FestivalBias Fair patch error: {ex.Message}", LogLevel.Warn);
        }
    }
}
