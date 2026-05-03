using System;
using System.Collections.Generic;
using DeluxeGrabberFix.Interfaces;
using StardewModdingAPI;

namespace DeluxeGrabberFix.Framework;

// Fluent wrapper around IGenericModConfigMenuApi. Each option call collapses
// to one line; tooltip getters are auto-attached when (and only when) i18n
// has a `{key}.tooltip` translation, so adding a new option is a one-touch
// change in default.json + one builder line in GmcmRegistration.
internal sealed class GmcmBuilder
{
    private readonly IGenericModConfigMenuApi _api;
    private readonly IManifest _manifest;
    private readonly ITranslationHelper _i18n;

    internal GmcmBuilder(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper i18n)
    {
        _api = api;
        _manifest = manifest;
        _i18n = i18n;
    }

    internal IGenericModConfigMenuApi Api => _api;
    internal IManifest Manifest => _manifest;

    private Func<string> Name(string key) => () => _i18n.Get(key);

    // Returns null when i18n has no `{key}.tooltip` entry, so GMCM omits the
    // tooltip rather than rendering "(no translation:...)" placeholder text.
    private Func<string> TooltipOrNull(string key) => ExplicitTooltipOrNull(key + ".tooltip");

    // Same gating applied to an explicit tooltip key (used when the tooltip lives
    // under a different key than `{labelKey}.tooltip`, e.g. page-link tooltips).
    private Func<string> ExplicitTooltipOrNull(string tooltipKey)
    {
        if (!_i18n.Get(tooltipKey).HasValue())
            return null;
        return () => _i18n.Get(tooltipKey);
    }

    internal GmcmBuilder Page(string pageId, string titleKey)
    {
        _api.AddPage(_manifest, pageId, Name(titleKey));
        return this;
    }

    internal GmcmBuilder PageLink(string pageId, string linkKey, string tooltipKey = null)
    {
        Func<string> tooltipGetter = tooltipKey != null
            ? ExplicitTooltipOrNull(tooltipKey)
            : TooltipOrNull(linkKey);
        _api.AddPageLink(_manifest, pageId, Name(linkKey), tooltipGetter);
        return this;
    }

    internal GmcmBuilder Section(string key)
    {
        _api.AddSectionTitle(_manifest, Name(key), TooltipOrNull(key));
        return this;
    }

    internal GmcmBuilder Paragraph(string key)
    {
        _api.AddParagraph(_manifest, Name(key));
        return this;
    }

    internal GmcmBuilder Bool(string key, Func<bool> get, Action<bool> set, string fieldId = null)
    {
        _api.AddBoolOption(_manifest, get, set, Name(key), TooltipOrNull(key), fieldId);
        return this;
    }

    internal GmcmBuilder Number(string key, Func<int> get, Action<int> set,
        int? min = null, int? max = null, int? interval = null)
    {
        _api.AddNumberOption(_manifest, get, set, Name(key), TooltipOrNull(key), min, max, interval);
        return this;
    }

    internal GmcmBuilder Keybind(string key, Func<SButton> get, Action<SButton> set)
    {
        _api.AddKeybind(_manifest, get, set, Name(key), TooltipOrNull(key));
        return this;
    }

    internal GmcmBuilder TextOption(string key, Func<string> get, Action<string> set)
    {
        _api.AddTextOption(_manifest, get, set, Name(key), TooltipOrNull(key));
        return this;
    }

    // Dropdown bound to an enum via the ModConfig.{Type}Dict / ReverseDict pair.
    // dropdownLabelPrefix:
    //   null      -> raw allowedValues are shown verbatim (no translation)
    //   ""        -> values translated via "dropdown.{value.ToLower()}"
    //   "flower-" -> values translated via "dropdown.flower-{value.ToLower()}"
    internal GmcmBuilder Dropdown<TEnum>(string key,
        Func<TEnum> get, Action<TEnum> set,
        IDictionary<TEnum, string> dict,
        IDictionary<string, TEnum> reverseDict,
        string[] allowedValues,
        string dropdownLabelPrefix = null)
    {
        Func<string, string> formatValue = null;
        if (dropdownLabelPrefix != null)
        {
            string prefix = dropdownLabelPrefix;
            formatValue = v => _i18n.Get($"dropdown.{prefix}{v.ToLower()}");
        }

        _api.AddTextOption(_manifest,
            () => dict[get()],
            v => set(reverseDict[v]),
            Name(key),
            TooltipOrNull(key),
            allowedValues,
            formatValue);
        return this;
    }

    internal GmcmBuilder OnFieldChanged(Action<string, object> onChange)
    {
        _api.OnFieldChanged(_manifest, onChange);
        return this;
    }
}
