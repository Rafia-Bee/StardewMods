using System.Runtime.CompilerServices;
using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

// InventoryEntry's public surface (Equals, GetHashCode, QualityName, QualityKey) is
// pure logic over the readonly QualifiedItemId/Quality fields, but the constructor
// reads them from a real Item. SDV's Item class is abstract, and its concrete
// subclasses pull on ItemRegistry/Game1 init that the test harness doesn't stand up.
//
// We bypass the constructor with RuntimeHelpers.GetUninitializedObject and seed the
// readonly fields by reflection. The tests still pin the real shipping methods --
// we just sidestep the dependency on SDV content init to construct the instances.
public class InventoryEntryTests
{
    private static InventoryEntry Make(string qualifiedItemId, int quality)
    {
        var entry = (InventoryEntry)RuntimeHelpers.GetUninitializedObject(typeof(InventoryEntry));
        typeof(InventoryEntry).GetField(nameof(InventoryEntry.QualifiedItemId))!.SetValue(entry, qualifiedItemId);
        typeof(InventoryEntry).GetField(nameof(InventoryEntry.Quality))!.SetValue(entry, quality);
        return entry;
    }

    public class EqualsAndHashCode
    {
        [Fact]
        public void SameIdAndQuality_AreEqual()
        {
            var a = Make("(O)128", 2);
            var b = Make("(O)128", 2);

            a.Equals(b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void SameIdDifferentQuality_AreNotEqual()
        {
            // Yield-tracking dictionaries key by (id, quality) so a Silver Wine and a
            // Gold Wine count separately. If quality were ignored, the HUD report
            // would lump them together.
            var silver = Make("(O)128", 1);
            var gold = Make("(O)128", 2);

            silver.Equals(gold).Should().BeFalse();
        }

        [Fact]
        public void DifferentIdSameQuality_AreNotEqual()
        {
            var a = Make("(O)128", 0);
            var b = Make("(O)129", 0);

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void NullObject_ReturnsFalse()
        {
            var a = Make("(O)128", 0);

            a.Equals(null).Should().BeFalse();
        }

        [Fact]
        public void DifferentType_ReturnsFalse()
        {
            var a = Make("(O)128", 0);

            a.Equals("(O)128").Should().BeFalse();
        }

        [Fact]
        public void DictionaryLookup_RoundTrips()
        {
            // The actual usage shape: GrabberManager builds Dictionary<InventoryEntry, int>
            // for yield diffs. Two entries with the same id+quality must hit the same
            // bucket regardless of insertion order.
            var dict = new System.Collections.Generic.Dictionary<InventoryEntry, int>
            {
                [Make("(O)128", 2)] = 5
            };

            dict.TryGetValue(Make("(O)128", 2), out int found).Should().BeTrue();
            found.Should().Be(5);

            dict.TryGetValue(Make("(O)128", 1), out _).Should().BeFalse();
            dict.TryGetValue(Make("(O)129", 2), out _).Should().BeFalse();
        }

        [Fact]
        public void NullQualifiedItemId_DoesNotThrowOnHashOrEquals()
        {
            // Defensive: a malformed Item could leave QualifiedItemId null. Equals
            // uses string equality (which handles null), and GetHashCode has an
            // explicit null guard. Pin both so a future tightening doesn't regress.
            var a = Make(null!, 0);
            var b = Make(null!, 0);

            a.GetHashCode().Should().Be(b.GetHashCode());
            a.Equals(b).Should().BeTrue();
        }
    }

    public class QualityName
    {
        [Theory]
        [InlineData(0, "Normal")]
        [InlineData(1, "Silver")]
        [InlineData(2, "Gold")]
        [InlineData(3, "Normal")] // 3 isn't a real SDV quality; falls through to Normal
        [InlineData(4, "Iridium")]
        [InlineData(99, "Normal")]
        public void MapsKnownQualities_FallsThroughOnUnknown(int quality, string expected)
        {
            var entry = Make("(O)128", quality);

            entry.QualityName.Should().Be(expected);
        }
    }

    public class QualityKey
    {
        [Theory]
        [InlineData(0, "log.quality-normal")]
        [InlineData(1, "log.quality-silver")]
        [InlineData(2, "log.quality-gold")]
        [InlineData(4, "log.quality-iridium")]
        [InlineData(7, "log.quality-normal")]
        public void MapsKnownQualities_FallsThroughOnUnknown(int quality, string expected)
        {
            // Pinned because the i18n keys here must match the entries in
            // i18n/default.json. Renaming a key in JSON without updating this
            // switch would silently produce raw key text in the HUD report.
            var entry = Make("(O)128", quality);

            entry.QualityKey.Should().Be(expected);
        }
    }
}
