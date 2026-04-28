using System;

namespace MoreQuests.Framework;

/// Subset of SpaceCore's `IApi` we need: registering custom Quest subclasses with the
/// SaveGame XmlSerializer so they survive a save/load round-trip. SpaceCore patches
/// SaveGame to thread these types into its serializer factory.
/// Types registered MUST be public and carry an [XmlType("Mods_...")] attribute.
public interface ISpaceCoreApi
{
    void RegisterSerializerType(Type type);
}
