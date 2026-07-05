using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace MoreQuestsFramework.Content;

// Parsed view of the Categories asset. Colors and skills are parsed once here (on load
// and on cache invalidation), not per draw. Built-ins are seeded so the default look and
// scaling are unchanged even before the asset loads; CP packs EditData the asset to recolor
// a built-in or add a new category, and the cache rebuilds on invalidation for hot-reload.
internal sealed class CategoryRegistry
{
    // Social's pair, used as the fallback for any unknown category or unparseable color.
    private static readonly Color DefaultPad = new(235, 195, 225);
    private static readonly Color DefaultPin = new(50, 110, 75);

    private readonly IMonitor _monitor;
    private Dictionary<string, (Color pad, Color pin)> _colors = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _skills = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _displayNames = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _padTextures = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _pinTextures = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CategoryIconSpec> _icons = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, float> _noteScales = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _popupBackgrounds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Color> _textColors = new(StringComparer.OrdinalIgnoreCase);

    public CategoryRegistry(IMonitor monitor)
    {
        _monitor = monitor;
        Rebuild(BuildBuiltinSeed());
    }

    // The nine built-ins as authorable entries. Doubles as the asset's seed (so the asset
    // is a copyable example) and as the parse fallback. Colors match the old hardcoded
    // pairs exactly; skills match the old GetSkillLevel switch (Festival/Seasonal/Social
    // scaled 0, so they seed "None"; Cooking routes to the installed cooking-skill mod).
    public static Dictionary<string, CategoryDefinition> BuildBuiltinSeed() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Animal"] = Seed("Animal", "#F4C39B", "#234B64", "Farming"),
        ["Cooking"] = Seed("Cooking", "#FAD78C", "#5F2D73", "Cooking"),
        ["Farming"] = Seed("Farming", "#D2E18C", "#822850", "Farming"),
        ["Festival"] = Seed("Festival", "#F0AFAF", "#325F46", "None"),
        ["Fishing"] = Seed("Fishing", "#AFD2EB", "#A04B1E", "Fishing"),
        ["Foraging"] = Seed("Foraging", "#B4DCA5", "#8C3250", "Foraging"),
        ["Mining"] = Seed("Mining", "#D2CDC8", "#822332", "Mining"),
        ["Seasonal"] = Seed("Seasonal", "#AFE1DC", "#9B3C2D", "None"),
        ["Social"] = Seed("Social", "#EBC3E1", "#326E4B", "None"),
    };

    private static CategoryDefinition Seed(string key, string pad, string pin, string skill) => new()
    {
        DisplayName = ModEntry.Translation?.Get($"category.{key.ToLowerInvariant()}").Default(key).ToString() ?? key,
        PadColor = pad,
        PinColor = pin,
        Skill = skill,
    };

    public void Rebuild(Dictionary<string, CategoryDefinition> asset)
    {
        var colors = new Dictionary<string, (Color, Color)>(StringComparer.OrdinalIgnoreCase);
        var skills = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var padTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pinTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var icons = new Dictionary<string, CategoryIconSpec>(StringComparer.OrdinalIgnoreCase);
        var noteScales = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var popupBackgrounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var textColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        if (asset != null)
        {
            foreach (var pair in asset)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    continue;
                Color pad = ParseColor(pair.Value.PadColor, pair.Key, "PadColor") ?? DefaultPad;
                Color pin = ParseColor(pair.Value.PinColor, pair.Key, "PinColor") ?? DefaultPin;
                colors[pair.Key] = (pad, pin);
                skills[pair.Key] = string.IsNullOrWhiteSpace(pair.Value.Skill) ? "None" : pair.Value.Skill;
                if (!string.IsNullOrWhiteSpace(pair.Value.DisplayName))
                    displayNames[pair.Key] = pair.Value.DisplayName.Trim();
                if (!string.IsNullOrWhiteSpace(pair.Value.PadTexture))
                    padTextures[pair.Key] = pair.Value.PadTexture.Trim();
                if (!string.IsNullOrWhiteSpace(pair.Value.PinTexture))
                    pinTextures[pair.Key] = pair.Value.PinTexture.Trim();
                var icon = BuildIconSpec(pair.Value);
                if (icon != null)
                    icons[pair.Key] = icon;
                if (pair.Value.NoteScale is > 0)
                    noteScales[pair.Key] = pair.Value.NoteScale.Value;
                if (!string.IsNullOrWhiteSpace(pair.Value.Font))
                    fonts[pair.Key] = pair.Value.Font.Trim();
                if (!string.IsNullOrWhiteSpace(pair.Value.PopupBackground))
                    popupBackgrounds[pair.Key] = pair.Value.PopupBackground.Trim();
                Color? textColor = ParseColor(pair.Value.TextColor, pair.Key, "TextColor");
                if (textColor.HasValue)
                    textColors[pair.Key] = textColor.Value;
            }
        }
        _colors = colors;
        _skills = skills;
        _displayNames = displayNames;
        _padTextures = padTextures;
        _pinTextures = pinTextures;
        _icons = icons;
        _noteScales = noteScales;
        _fonts = fonts;
        _popupBackgrounds = popupBackgrounds;
        _textColors = textColors;
    }

    public (Color pad, Color pin) ColorsFor(string? category)
        => !string.IsNullOrEmpty(category) && _colors.TryGetValue(category, out var c) ? c : (DefaultPad, DefaultPin);

    public string SkillFor(string? category)
        => !string.IsNullOrEmpty(category) && _skills.TryGetValue(category, out var s) ? s : "None";

    // Every category id the registry knows (the built-in nine plus any a CP pack added to
    // the Categories asset). Snapshot, so the caller can register GMCM toggles off it.
    public IReadOnlyList<string> KnownCategories() => new List<string>(_colors.Keys);

    // Authored display name for a category, or null when the asset didn't set one (the
    // caller falls back to the category id or a translation).
    public string? DisplayNameFor(string? category)
        => !string.IsNullOrEmpty(category) && _displayNames.TryGetValue(category, out var n) ? n : null;

    // Category pad/pin texture override, or null to use the board / framework default.
    public string? PadTextureFor(string? category)
        => !string.IsNullOrEmpty(category) && _padTextures.TryGetValue(category, out var t) ? t : null;

    public string? PinTextureFor(string? category)
        => !string.IsNullOrEmpty(category) && _pinTextures.TryGetValue(category, out var t) ? t : null;

    // Category icon spec, or the default (giver portrait, bottom-left, 0.28 scale).
    public CategoryIconSpec IconFor(string? category)
        => !string.IsNullOrEmpty(category) && _icons.TryGetValue(category, out var s) ? s : CategoryIconSpec.Default;

    // Note-size multiplier for a notice in this category, or 1.0 (no change).
    public float NoteScaleFor(string? category)
        => !string.IsNullOrEmpty(category) && _noteScales.TryGetValue(category, out var s) ? s : 1f;

    // Notice popup body font name ("Dialogue"/"Small"/"Tiny" or an asset), or null for the default.
    public string? FontFor(string? category)
        => !string.IsNullOrEmpty(category) && _fonts.TryGetValue(category, out var f) ? f : null;

    // Notice popup parchment skin asset, or null to reuse the board background.
    public string? PopupBackgroundFor(string? category)
        => !string.IsNullOrEmpty(category) && _popupBackgrounds.TryGetValue(category, out var t) ? t : null;

    // Notice popup text color, or null for the game default.
    public Color? TextColorFor(string? category)
        => !string.IsNullOrEmpty(category) && _textColors.TryGetValue(category, out var c) ? c : null;

    // Returns null when the entry sets nothing icon-related, so the default spec is shared.
    private static CategoryIconSpec? BuildIconSpec(CategoryDefinition def)
    {
        bool any = !string.IsNullOrWhiteSpace(def.Icon)
            || def.IconSource is { Length: >= 4 }
            || def.IconScale.HasValue
            || !string.IsNullOrWhiteSpace(def.IconAnchor)
            || (def.IconX.HasValue && def.IconY.HasValue);
        if (!any)
            return null;

        Rectangle? source = def.IconSource is { Length: >= 4 }
            ? new Rectangle(def.IconSource[0], def.IconSource[1], def.IconSource[2], def.IconSource[3])
            : null;
        return new CategoryIconSpec
        {
            Value = string.IsNullOrWhiteSpace(def.Icon) ? null : def.Icon.Trim(),
            Source = source,
            Scale = def.IconScale is > 0 ? def.IconScale.Value : 0.28f,
            Anchor = string.IsNullOrWhiteSpace(def.IconAnchor) ? "BottomLeft" : def.IconAnchor.Trim(),
            X = def.IconX,
            Y = def.IconY,
        };
    }

    // Accepts #RRGGBB, #RRGGBBAA, or R,G,B (0-255). Returns null for an empty value (caller
    // uses the default silently) and warns once-per-call on a non-empty unparseable value.
    private Color? ParseColor(string? raw, string categoryId, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string s = raw.Trim();
        try
        {
            if (s.StartsWith("#"))
            {
                string hex = s.Substring(1);
                if (hex.Length == 6 || hex.Length == 8)
                {
                    int r = HexByte(hex, 0);
                    int g = HexByte(hex, 2);
                    int b = HexByte(hex, 4);
                    int a = hex.Length == 8 ? HexByte(hex, 6) : 255;
                    return new Color(r, g, b, a);
                }
            }
            else if (s.Contains(','))
            {
                var parts = s.Split(',');
                if (parts.Length == 3
                    && byte.TryParse(parts[0].Trim(), out byte r)
                    && byte.TryParse(parts[1].Trim(), out byte g)
                    && byte.TryParse(parts[2].Trim(), out byte b))
                {
                    return new Color(r, g, b);
                }
            }
        }
        catch
        {
            // Falls through to the warn + null below.
        }

        _monitor.Log($"Category '{categoryId}': {field} value '{raw}' isn't a valid color (#RRGGBB, #RRGGBBAA, or R,G,B). Falling back to the default.", LogLevel.Warn);
        return null;
    }

    private static int HexByte(string hex, int start)
        => int.Parse(hex.Substring(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
