using System.Reflection;
using DeluxeGrabberFix;
using DeluxeGrabberFix.Grabbers;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

// Audit §4.9: smoke tests for the MachineGrabber dispatch tables. The tables are static
// and pure (no Game1 dependency), so we can reflect into them and assert structural
// properties without standing up a save. Catches typos in the gate predicates and
// drift between the two tables.
public class MachineGrabberDispatchTests
{
    private static (System.Collections.IDictionary Table, System.Type EntryType) GetTable(string fieldName)
    {
        var field = typeof(MachineGrabber).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new System.InvalidOperationException("MachineGrabber should expose " + fieldName);
        var entryType = field.FieldType.GetGenericArguments()[1];
        var dict = (System.Collections.IDictionary)field.GetValue(null)!;
        return (dict, entryType);
    }

    [Fact]
    public void StandardMachines_HasNoEmptyNamesOrNullGates()
    {
        var (table, entryType) = GetTable("StandardMachines");
        table.Count.Should().BeGreaterThan(20, "the bulk of the if/else chain landed here");

        var settings = new ModConfig.MachineSettings();
        var nameField = entryType.GetProperty("Name")!;
        var gateField = entryType.GetProperty("Gate")!;

        foreach (System.Collections.DictionaryEntry kvp in table)
        {
            var name = (string)nameField.GetValue(kvp.Value)!;
            var gate = (System.Delegate)gateField.GetValue(kvp.Value)!;
            name.Should().NotBeNullOrWhiteSpace($"entry '{kvp.Key}' should have a debug name");
            gate.Should().NotBeNull($"entry '{kvp.Key}' should have a gate");
            // Invoking the gate with a default MachineSettings exercises the lambda
            // body; defaults are mostly true, so this catches null-deref typos.
            var ok = (bool)gate.DynamicInvoke(settings)!;
            ok.Should().BeTrue($"entry '{kvp.Key}' default gate value should be true (defaults are opt-out)");
        }
    }

    [Fact]
    public void MpsMachines_FragmentsAreDistinctEnoughToNotOverlap()
    {
        var (table, _) = GetTable("MpsMachines");
        // Only assertion we can make at the smoke-test level: every fragment is a
        // distinct, non-empty string. Substring overlap (e.g. "WormBin" matching
        // "DeluxeWormBin") is intentional in the existing data and both targets share
        // the same gate, so substring overlap alone isn't a bug -- noted in the
        // dispatch comment.
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (System.Collections.DictionaryEntry kvp in table)
        {
            var key = (string)kvp.Key;
            key.Should().NotBeNullOrWhiteSpace();
            keys.Add(key).Should().BeTrue($"fragment '{key}' must be unique");
        }
    }

    [Fact]
    public void StandardMachines_AndMpsMachines_HaveOverlapForArtisanMachines()
    {
        var (standard, _) = GetTable("StandardMachines");
        var (mps, _) = GetTable("MpsMachines");

        // Spot check a few that should have both paths registered (vanilla + MPS variant).
        // If a future contributor removes one but not the other, the test surfaces the drift.
        standard.Contains("(BC)12").Should().BeTrue("Keg vanilla ID should be in StandardMachines");
        mps.Contains("Keg").Should().BeTrue("Keg MPS fragment should be in MpsMachines");

        standard.Contains("(BC)10").Should().BeTrue("BeeHouse vanilla ID should be in StandardMachines");
        mps.Contains("BeeHouse").Should().BeTrue("BeeHouse MPS fragment should be in MpsMachines");
    }
}
