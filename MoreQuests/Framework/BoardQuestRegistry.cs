using System.Collections.Generic;
using MoreQuests.Framework.Quests;
using MoreQuests.Framework.Quests.Vanilla;
using MoreQuests.Framework.Registry;
using StardewModdingAPI;

namespace MoreQuests.Framework;

/// Static facade over a runtime `QuestRegistry` instance. Preserves the original
/// `BoardQuestRegistry.All` / `WithKind(...)` call sites while moving the actual
/// definition list off a hard-coded array. The current 15 built-ins are registered
/// once at GameLaunched via `Initialize`.
internal static class BoardQuestRegistry
{
    private static QuestRegistry? _instance;

    public static IReadOnlyList<IQuestDefinition> All =>
        _instance?.All ?? System.Array.Empty<IQuestDefinition>();

    public static IEnumerable<IQuestDefinition> WithKind(PostingKind kind) =>
        _instance?.WithKind(kind) ?? System.Linq.Enumerable.Empty<IQuestDefinition>();

    /// Builds the registry instance and registers all built-in quests. Idempotent;
    /// repeat calls clear the registry and re-register from scratch (useful for tests).
    public static void Initialize(IMonitor monitor)
    {
        _instance = new QuestRegistry(monitor);

        _instance.Register(new VanillaItemDelivery());
        _instance.Register(new VanillaResourceCollection());
        _instance.Register(new VanillaSlayMonster());
        _instance.Register(new VanillaFishing());
        _instance.Register(new BasicCropDelivery());
        _instance.Register(new SimpleFishingRequest());
        _instance.Register(new BasicSlimeClearing());
        _instance.Register(new BarDelivery());
        _instance.Register(new SeasonalForaging());
        _instance.Register(new ElliottPoemInspiration());
        _instance.Register(new CheckOnGeorge());
        _instance.Register(new HaySupplyRun());
        _instance.Register(new BeachCleanup());
        _instance.Register(new SpringTea());
        _instance.Register(new CravingDish());
    }

    /// Direct access to the underlying registry for code that needs Register / Freeze.
    /// Will become the public-API entrypoint in later phases.
    public static QuestRegistry? Instance => _instance;
}
