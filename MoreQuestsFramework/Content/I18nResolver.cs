using System.Text.RegularExpressions;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

/// Resolves `{i18n:key.subkey}` tokens against a SMAPI translation helper.
/// Scoped per content pack so each pack's `quests.json` looks up keys in
/// its own `i18n/default.json` (plan.md §5.6). Unknown keys fall through
/// to the original token text so authors get a visible cue in-game.
internal static class I18nResolver
{
    private static readonly Regex Token = new(@"\{i18n:([^}]+)\}", RegexOptions.Compiled);

    public static string Resolve(string? input, ITranslationHelper translation)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;
        if (translation == null || !input.Contains("{i18n:"))
            return input;

        return Token.Replace(input, m =>
        {
            string key = m.Groups[1].Value.Trim();
            var t = translation.Get(key);
            return t.HasValue() ? t.ToString() : m.Value;
        });
    }
}
