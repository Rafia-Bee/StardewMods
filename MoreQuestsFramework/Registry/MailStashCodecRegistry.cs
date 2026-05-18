using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Registry;

// Lets the framework round-trip custom Quest subclasses through the mail-stash DTO.
// Vanilla-shape postings already serialise fine; only quests with extra NetFields
// (AdventureQuest, MoreQuestsShipQuest, plus any consumer-mod subclass) need a codec.
internal sealed class MailStashCodecRegistry
{
    private readonly Dictionary<string, Codec> _byKind = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _byType = new();
    private readonly IMonitor _monitor;

    public MailStashCodecRegistry(IMonitor monitor) { _monitor = monitor; }

    private sealed record Codec(Type QuestType, Func<Quest, IList<string>> Encode, Func<IList<string>, Quest?> Decode);

    public void Register(string kind, Type questType, Func<Quest, IList<string>> encode, Func<IList<string>, Quest?> decode)
    {
        if (string.IsNullOrEmpty(kind) || questType == null || encode == null || decode == null)
        {
            _monitor.Log("RegisterMailStashCodec called with null or empty argument; ignored.", LogLevel.Warn);
            return;
        }
        if (!typeof(Quest).IsAssignableFrom(questType))
        {
            _monitor.Log($"RegisterMailStashCodec('{kind}') ignored: '{questType.FullName}' is not a Quest subclass.", LogLevel.Warn);
            return;
        }
        if (_byKind.ContainsKey(kind))
        {
            _monitor.Log($"RegisterMailStashCodec('{kind}') ignored: kind is already registered.", LogLevel.Warn);
            return;
        }
        if (_byType.ContainsKey(questType))
        {
            _monitor.Log($"RegisterMailStashCodec for type '{questType.Name}' ignored: a codec is already registered for that type.", LogLevel.Warn);
            return;
        }
        _byKind[kind] = new Codec(questType, encode, decode);
        _byType[questType] = kind;
        _monitor.Log($"Mail-stash codec '{kind}' bound to Quest subclass '{questType.Name}'.", LogLevel.Trace);
    }

    public bool TryEncode(Quest quest, out string kind, out List<string> payload)
    {
        kind = string.Empty;
        payload = new List<string>();
        if (quest == null) return false;
        if (!_byType.TryGetValue(quest.GetType(), out var k))
            return false;
        kind = k;
        var produced = _byKind[k].Encode(quest);
        if (produced != null)
            payload.AddRange(produced);
        return true;
    }

    public bool TryDecode(string kind, IList<string> payload, out Quest? quest)
    {
        quest = null;
        if (string.IsNullOrEmpty(kind) || !_byKind.TryGetValue(kind, out var codec))
            return false;
        quest = codec.Decode(payload ?? new List<string>());
        return quest != null;
    }
}
