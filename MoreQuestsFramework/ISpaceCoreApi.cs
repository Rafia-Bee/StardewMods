using System;

namespace MoreQuestsFramework;

// Registered types MUST be public and carry [XmlType("Mods_...")].
public interface ISpaceCoreApi
{
    void RegisterSerializerType(Type type);
}
