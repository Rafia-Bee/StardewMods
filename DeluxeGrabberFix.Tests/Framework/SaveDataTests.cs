using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

// Audit step 4: SaveData.SchemaVersion gates one-shot per-save migrations
// (specialized-grabber conversion + RepairStuckMachines). The contract this
// suite pins:
//   1. Fresh SaveData defaults to SchemaVersion 0 -- so legacy save files
//      (which never wrote the field) deserialize as "needs migration".
//   2. Newtonsoft round-trips the field -- the bump-then-write step in
//      RunPerSaveMigrations actually persists.
//   3. CurrentSchemaVersion is the bump target. If a future migration is
//      added, this test breaks loudly so the bump and the test stay in sync.
public class SaveDataTests
{
    [Fact]
    public void Default_SchemaVersion_IsZero()
    {
        var data = new SaveData();
        data.SchemaVersion.Should().Be(0);
    }

    [Fact]
    public void LegacyJson_WithoutSchemaVersion_DeserializesAsZero()
    {
        // What pre-step-4 saves look like on disk: no SchemaVersion field.
        // Newtonsoft must fall back to the property default (0) so legacy
        // saves get classified as "needs migration" on first load.
        const string legacyJson = "{\"AutoSkippedLocations\":[],\"ManuallyManagedLocations\":[],\"BlacklistedLocations\":[]}";

        var data = JsonConvert.DeserializeObject<SaveData>(legacyJson);

        data.Should().NotBeNull();
        data!.SchemaVersion.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_PreservesSchemaVersion()
    {
        var original = new SaveData { SchemaVersion = SaveData.CurrentSchemaVersion };
        original.AutoSkippedLocations.Add("Town");

        string json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<SaveData>(json);

        restored.Should().NotBeNull();
        restored!.SchemaVersion.Should().Be(SaveData.CurrentSchemaVersion);
        restored.AutoSkippedLocations.Should().BeEquivalentTo(new[] { "Town" });
    }

    [Fact]
    public void CurrentSchemaVersion_IsOne()
    {
        // Pinning the bump target. When step-4-style migrations are added
        // (or removed), bump CurrentSchemaVersion in lockstep with this test
        // so we never silently re-run or silently skip a migration.
        SaveData.CurrentSchemaVersion.Should().Be(1);
    }
}
