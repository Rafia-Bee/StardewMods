using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace QuestJournal.Menu;

// Holds the journal's color theme (tints, divider, header).
// Loads colors from a game-content asset so packs can override them, falls back
// to defaults, and parses hex color strings (3, 4, 6, or 8 digits).
public static class JournalTheme
{
    public const string KeySelectedTint = "SelectedTint";
    public const string KeyHoverTint = "HoverTint";
    public const string KeyDividerColor = "DividerColor";
    public const string KeyHeaderColor = "HeaderColor";

    private const string DefaultSelectedTint = "#00000066";
    private const string DefaultHoverTint = "#00000033";
    private const string DefaultDividerColor = "#0000004D";
    private const string DefaultHeaderColor = "#113366FF";

    public static Color SelectedTint { get; private set; } = Parse(DefaultSelectedTint);
    public static Color HoverTint { get; private set; } = Parse(DefaultHoverTint);
    public static Color DividerColor { get; private set; } = Parse(DefaultDividerColor);
    public static Color HeaderColor { get; private set; } = Parse(DefaultHeaderColor);

    public static string AssetName(string uniqueId) => $"Mods/{uniqueId}/Theme";

    public static Dictionary<string, string> BuildDefaults() => new()
    {
        [KeySelectedTint] = DefaultSelectedTint,
        [KeyHoverTint] = DefaultHoverTint,
        [KeyDividerColor] = DefaultDividerColor,
        [KeyHeaderColor] = DefaultHeaderColor,
    };

    public static void Reload(IModHelper helper, string uniqueId)
    {
        Dictionary<string, string>? data = null;
        try { data = helper.GameContent.Load<Dictionary<string, string>>(AssetName(uniqueId)); }
        catch { /* fall through to defaults below */ }

        SelectedTint = Pick(data, KeySelectedTint, DefaultSelectedTint);
        HoverTint = Pick(data, KeyHoverTint, DefaultHoverTint);
        DividerColor = Pick(data, KeyDividerColor, DefaultDividerColor);
        HeaderColor = Pick(data, KeyHeaderColor, DefaultHeaderColor);
    }

    private static Color Pick(IReadOnlyDictionary<string, string>? data, string key, string fallback)
    {
        if (data != null && data.TryGetValue(key, out var raw) && TryParse(raw, out var c))
            return c;
        return Parse(fallback);
    }

    private static Color Parse(string hex) => TryParse(hex, out var c) ? c : Color.White;

    public static bool TryParse(string? hex, out Color color)
    {
        color = Color.White;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string s = hex.Trim();
        if (s.StartsWith("#")) s = s.Substring(1);

        int r, g, b, a = 255;
        try
        {
            switch (s.Length)
            {
                case 3:
                    r = Nibble(s[0]); g = Nibble(s[1]); b = Nibble(s[2]);
                    break;
                case 4:
                    r = Nibble(s[0]); g = Nibble(s[1]); b = Nibble(s[2]); a = Nibble(s[3]);
                    break;
                case 6:
                    r = Octet(s, 0); g = Octet(s, 2); b = Octet(s, 4);
                    break;
                case 8:
                    r = Octet(s, 0); g = Octet(s, 2); b = Octet(s, 4); a = Octet(s, 6);
                    break;
                default:
                    return false;
            }
        }
        catch (System.FormatException) { return false; }

        color = new Color(r, g, b, a);
        return true;
    }

    private static int Nibble(char c) => Hex(c) * 17;

    private static int Octet(string s, int i) => Hex(s[i]) * 16 + Hex(s[i + 1]);

    private static int Hex(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        throw new System.FormatException($"Bad hex digit '{c}'.");
    }
}
