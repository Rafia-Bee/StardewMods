using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Registry;

internal sealed class QuestRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, IQuestDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IQuestDefinition> _ordered = new();
    private readonly Dictionary<string, TriggerSource> _sourceOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TriggerSource> _pendingOverrides = new(StringComparer.OrdinalIgnoreCase);
    // Target board for a quest routed to CustomBoard by override, keyed by quest id and
    // already namespaced as owner/board. Read lazily by the custom-board draw, so order
    // against registration doesn't matter. Survives CP hot reload like _sourceOverrides.
    private readonly Dictionary<string, string> _boardOverrides = new(StringComparer.OrdinalIgnoreCase);
    private bool _frozen;

    public QuestRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public IReadOnlyList<IQuestDefinition> All => _ordered;

    public IEnumerable<IQuestDefinition> WithKind(PostingKind kind)
    {
        for (int i = 0; i < _ordered.Count; i++)
        {
            if (_ordered[i].Kind == kind)
                yield return _ordered[i];
        }
    }

    // Returns true when the def took. False means the call was rejected (frozen registry
    // or duplicate id), with the reason logged at Warn. Consumer mods can branch on the
    // return value to surface their own failures instead of guessing from log output.
    public bool Register(IQuestDefinition def)
    {
        if (_frozen)
        {
            _monitor.Log($"QuestRegistry rejected '{def.Id}': registry is frozen for this session.", LogLevel.Warn);
            return false;
        }
        if (_byId.ContainsKey(def.Id))
        {
            _monitor.Log($"QuestRegistry rejected duplicate registration for '{def.Id}'.", LogLevel.Warn);
            return false;
        }
        _byId[def.Id] = def;
        _ordered.Add(def);

        // Drain any buffered override that targeted this id before its owner mod ran.
        if (_pendingOverrides.TryGetValue(def.Id, out var pending))
        {
            string? incompat = ValidateOverrideCompat(def, pending);
            _pendingOverrides.Remove(def.Id);
            if (incompat != null)
                _monitor.Log($"Buffered OverrideSource for '{def.Id}' = {pending} dropped: {incompat}", LogLevel.Warn);
            else
            {
                _sourceOverrides[def.Id] = pending;
                ModEntry.LogDebug($"Applied buffered source override on '{def.Id}': {pending}.");
            }
        }
        return true;
    }

    public bool TryGet(string id, out IQuestDefinition? def) => _byId.TryGetValue(id, out def!);

    // Allowed both during the registration window and after freeze. A removed quest
    // stops appearing in pipeline draws on the next DayStarted, in-flight quests
    // already in the player's journal keep working (their state lives on the Quest
    // instance, not the registry).
    public bool Unregister(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return false;
        if (!_byId.TryGetValue(definitionId, out var def))
        {
            _monitor.Log($"Unregister('{definitionId}') ignored: no such quest registered.", LogLevel.Warn);
            return false;
        }
        _byId.Remove(definitionId);
        _ordered.Remove(def);
        _sourceOverrides.Remove(definitionId);
        _pendingOverrides.Remove(definitionId);
        ModEntry.LogDebug($"Quest '{definitionId}' unregistered.");
        return true;
    }

    // Allowed both during the registration window and after freeze (metadata only,
    // doesn't add or remove definitions). Calls during the registration window that
    // target an id whose owner mod hasn't called RegisterQuest yet are buffered and
    // replayed when Register sees the matching id. Buffered overrides that never
    // resolve get a Warn at Freeze time.
    //
    // Rejects the override when the def's existing TriggerInfo can't support the new
    // source (e.g. switching to NpcDialogue with no Trigger.Npc set). Otherwise the
    // runtime would drop the posting silently each day with no obvious signal.
    public void OverrideSource(string definitionId, TriggerSource source)
    {
        if (string.IsNullOrEmpty(definitionId))
            return;
        if (_byId.TryGetValue(definitionId, out var def))
        {
            string? incompat = ValidateOverrideCompat(def, source);
            if (incompat != null)
            {
                _monitor.Log($"OverrideSource('{definitionId}', {source}) ignored: {incompat}", LogLevel.Warn);
                return;
            }
            _sourceOverrides[definitionId] = source;
            ModEntry.LogDebug($"Quest '{definitionId}' source override set to {source}.");
            return;
        }
        if (_frozen)
        {
            _monitor.Log($"OverrideSource('{definitionId}', {source}) ignored: no such quest registered.", LogLevel.Warn);
            return;
        }
        // Pending overrides can't validate against a TriggerInfo yet (the def isn't
        // here). Compat check runs in Register when the buffered entry resolves.
        _pendingOverrides[definitionId] = source;
        ModEntry.LogDebug($"Buffered source override on '{definitionId}' = {source} until its owner registers.");
    }

    private static string? ValidateOverrideCompat(IQuestDefinition def, TriggerSource source)
    {
        var t = def.Trigger ?? Triggers.TriggerInfo.Default;
        return source switch
        {
            TriggerSource.NpcDialogue when string.IsNullOrWhiteSpace(t.Npc)
                => "NpcDialogue requires Trigger.Npc to be set on the definition.",
            TriggerSource.BuildingBuilt when string.IsNullOrWhiteSpace(t.Building)
                => "BuildingBuilt requires Trigger.Building to be set on the definition.",
            TriggerSource.MailReceived when string.IsNullOrWhiteSpace(t.Flag)
                => "MailReceived requires Trigger.Flag to be set on the definition.",
            TriggerSource.WeatherForecast when string.IsNullOrWhiteSpace(t.Weather)
                => "WeatherForecast requires Trigger.Weather to be set on the definition.",
            TriggerSource.Periodic when (t.EveryDays ?? 0) <= 0
                => "Periodic requires Trigger.EveryDays > 0 on the definition.",
            TriggerSource.DateLocked when string.IsNullOrWhiteSpace(t.Date)
                => "DateLocked requires Trigger.Date to be set on the definition.",
            TriggerSource.DateRange when (string.IsNullOrWhiteSpace(t.From) || string.IsNullOrWhiteSpace(t.To))
                => "DateRange requires both Trigger.From and Trigger.To to be set on the definition.",
            TriggerSource.OneShot when string.IsNullOrWhiteSpace(t.When)
                => "OneShot requires Trigger.When to be set on the definition.",
            TriggerSource.SpecialOrder when string.IsNullOrWhiteSpace(t.StartDate) && (t.Weight ?? 0) <= 0
                => "SpecialOrder requires either Trigger.StartDate, or Trigger.Weight > 0 for the cooldown-only Sunday roll path.",
            TriggerSource.Custom when string.IsNullOrWhiteSpace(t.Custom)
                => "Custom requires Trigger.Custom (handler id) to be set on the definition.",
            _ => null,
        };
    }

    public TriggerSource EffectiveSource(IQuestDefinition def)
    {
        if (def == null) return TriggerSource.DailyBoard;
        return _sourceOverrides.TryGetValue(def.Id, out var s) ? s : def.Source;
    }

    // Names the board a quest routes to when it has no per-def Trigger.CustomBoardId (e.g. a
    // framework-owned quest sent to a custom board by OverrideTriggerSource). The board
    // key is expected pre-namespaced as owner/board.
    public void OverrideBoard(string definitionId, string boardKey)
    {
        if (string.IsNullOrEmpty(definitionId) || string.IsNullOrWhiteSpace(boardKey))
            return;
        _boardOverrides[definitionId] = boardKey;
        ModEntry.LogDebug($"Quest '{definitionId}' board override set to '{boardKey}'.");
    }

    public string? EffectiveBoard(IQuestDefinition def)
    {
        if (def == null) return null;
        return _boardOverrides.TryGetValue(def.Id, out var key) ? key : null;
    }

    public void Freeze()
    {
        _frozen = true;
        if (_pendingOverrides.Count > 0)
        {
            foreach (var (id, source) in _pendingOverrides)
                _monitor.Log($"Buffered OverrideSource for '{id}' = {source} never resolved: no quest with that id was registered.", LogLevel.Warn);
            _pendingOverrides.Clear();
        }
    }

    // Keeps _sourceOverrides intact: those were set by consumer mods during their
    // one-time GameLaunched and don't get re-applied on hot reload of CP assets.
    public void Clear()
    {
        _byId.Clear();
        _ordered.Clear();
        _pendingOverrides.Clear();
        _frozen = false;
    }

    public IReadOnlyList<string> RegisteredIds() => _ordered.Select(d => d.Id).ToList();
}
