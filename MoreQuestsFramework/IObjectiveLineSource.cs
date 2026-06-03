using System.Collections.Generic;

namespace MoreQuestsFramework;

// A quest that wants to show more than one objective line (and have those lines
// reflect live state) implements this. The GetObjectiveDescriptions patch reads it
// so the vanilla quest log renders every line, and IMoreQuestsApi.GetObjectiveLines
// exposes the same list so a journal UI can render them without knowing the concrete
// quest type. Lets a third-party Quest subclass get multi-line objectives without
// taking its own Harmony patch on the non-virtual GetObjectiveDescriptions.
public interface IObjectiveLineSource
{
    IReadOnlyList<string> GetObjectiveLines();
}
