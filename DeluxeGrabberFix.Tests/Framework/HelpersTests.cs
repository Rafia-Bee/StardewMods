using System.Collections.Generic;
using System.Linq;
using DeluxeGrabberFix;
using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

public class HelpersTests
{
    // GetNearbyObjectsToTile only uses pair.Key (Vector2). It never dereferences
    // the Object value, so passing null is safe and lets us test the pure
    // distance/range logic without touching Stardew's content pipeline.
    private static KeyValuePair<Vector2, Object> At(int x, int y)
        => new(new Vector2(x, y), null!);

    public class GetNearbyObjectsToTile
    {
        [Fact]
        public void NegativeRange_ReturnsAllObjects_Unfiltered()
        {
            var grabbers = new[] { At(0, 0), At(50, 50), At(-100, -100) };

            var result = Helpers.GetNearbyObjectsToTile(
                tile: new Vector2(5, 5),
                objects: grabbers,
                range: -1,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk).ToList();

            result.Should().BeEquivalentTo(grabbers);
        }

        [Fact]
        public void WalkMode_FiltersByManhattanDistance()
        {
            var center = new Vector2(10, 10);
            var grabbers = new[]
            {
                At(10, 10), // distance 0
                At(11, 10), // distance 1
                At(12, 12), // distance 4
                At(13, 13), // distance 6 (excluded at range=5)
            };

            var result = Helpers.GetNearbyObjectsToTile(
                tile: center,
                objects: grabbers,
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk).ToList();

            result.Select(p => p.Key).Should().BeEquivalentTo(new[]
            {
                new Vector2(10, 10),
                new Vector2(11, 10),
                new Vector2(12, 12)
            });
        }

        [Fact]
        public void WalkMode_DistanceEqualToRange_IsIncluded()
        {
            var grabbers = new[] { At(15, 10) }; // distance 5 from (10,10)

            var result = Helpers.GetNearbyObjectsToTile(
                tile: new Vector2(10, 10),
                objects: grabbers,
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk).ToList();

            result.Should().HaveCount(1);
        }

        [Fact]
        public void SquareMode_FiltersByChebyshevBoundingBox()
        {
            var center = new Vector2(10, 10);
            var grabbers = new[]
            {
                At(10, 10), // inside
                At(13, 13), // inside (within 3x3 around center+3 box)
                At(13, 14), // outside (y delta 4)
                At(14, 13), // outside (x delta 4)
            };

            var result = Helpers.GetNearbyObjectsToTile(
                tile: center,
                objects: grabbers,
                range: 3,
                rangeMode: ModConfig.HarvestCropsRangeMode.Square).ToList();

            result.Select(p => p.Key).Should().BeEquivalentTo(new[]
            {
                new Vector2(10, 10),
                new Vector2(13, 13)
            });
        }

        [Fact]
        public void SquareMode_IncludesDiagonalThatWalkExcludes()
        {
            // Square treats (3,3) as inside range 3 (max delta = 3).
            // Walk excludes it (manhattan distance = 6).
            var grabbers = new[] { At(13, 13) };
            var center = new Vector2(10, 10);

            var square = Helpers.GetNearbyObjectsToTile(
                center, grabbers, range: 3,
                ModConfig.HarvestCropsRangeMode.Square).ToList();
            var walk = Helpers.GetNearbyObjectsToTile(
                center, grabbers, range: 3,
                ModConfig.HarvestCropsRangeMode.Walk).ToList();

            square.Should().HaveCount(1);
            walk.Should().BeEmpty();
        }

        [Fact]
        public void EmptyInput_ReturnsEmpty()
        {
            var result = Helpers.GetNearbyObjectsToTile(
                tile: Vector2.Zero,
                objects: System.Array.Empty<KeyValuePair<Vector2, Object>>(),
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk).ToList();

            result.Should().BeEmpty();
        }

        // AUDIT §5.3: today the helper throws on unknown rangeMode. This test
        // documents the current contract; if/when the audit fix lands (defensive
        // fallback to the unfiltered enumerable + debug log), update this test.
        [Fact]
        public void UnknownRangeMode_ThrowsToday()
        {
            var grabbers = new[] { At(0, 0) };
            var bogusMode = (ModConfig.HarvestCropsRangeMode)999;

            // Materialize via ToList() because the helper returns a lazy IEnumerable
            // and the throw is inside the deferred Where lambda. (Audit §1.6 also.)
            System.Action act = () => Helpers.GetNearbyObjectsToTile(
                Vector2.Zero, grabbers, range: 5, bogusMode).ToList();

            act.Should().Throw<System.Exception>();
        }
    }
}
