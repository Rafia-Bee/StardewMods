using System;
using HarmonyLib;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Luau: bumps "switchEvent governorReactionN" (N=0..6) by the bias magnitude, clamping
// to 5. Never overwrites 6 (Mayor's Shorts gag).
// Fair: adds the bias to grangeScore, except when score < 0 (Mayor's Shorts -666).
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
