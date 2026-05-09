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

        // AUDIT §5.3: previously threw on unknown rangeMode. Fix replaced the
        // throw with a defensive fallback that returns the unfiltered enumerable
        // (matches the range == -1 short-circuit). A future enum addition that
        // forgets to update the switch degrades to no range filter rather than
        // crashing mid-grab.
        [Fact]
        public void UnknownRangeMode_FallsThroughUnfiltered()
        {
            var grabbers = new[] { At(0, 0), At(100, 100) };
            var bogusMode = (ModConfig.HarvestCropsRangeMode)999;

            var result = Helpers.GetNearbyObjectsToTile(
                Vector2.Zero, grabbers, range: 5, bogusMode).ToList();

            // Both entries pass through; (100, 100) would have been filtered out by
            // either Walk or Square mode at range 5, so its presence pins that the
            // fallback is genuinely the unfiltered list.
            result.Should().HaveCount(2);
        }
    }

    // Audit §2.10: the partition helper used by MapGrabber to keep same-location
    // grabbers ahead of cross-location ones. Predicate is injected so tests don't
    // need a real GameLocation to exercise the bucket logic.
    public class SortSameLocationFirst
    {
        [Fact]
        public void EmptySource_ReturnsEmpty()
        {
            var result = Helpers.SortSameLocationFirst(
                System.Array.Empty<KeyValuePair<Vector2, Object>>(),
                _ => true);

            result.Should().BeEmpty();
        }

        [Fact]
        public void AllSameLocation_PreservesInputOrder()
        {
            var input = new[] { At(1, 1), At(2, 2), At(3, 3) };

            var result = Helpers.SortSameLocationFirst(input, _ => true);

            result.Select(p => p.Key).Should().ContainInOrder(input.Select(p => p.Key));
        }

        [Fact]
        public void AllCrossLocation_PreservesInputOrder()
        {
            var input = new[] { At(1, 1), At(2, 2), At(3, 3) };

            var result = Helpers.SortSameLocationFirst(input, _ => false);

            result.Select(p => p.Key).Should().ContainInOrder(input.Select(p => p.Key));
        }

        [Fact]
        public void Mixed_SameLocationFirst_RelativeOrderPreservedWithinEachBucket()
        {
            // Tiles 1,3,5 are same-location; tiles 2,4 are cross-location. Stable
            // partition: same first (1,3,5), then cross (2,4) -- not (1,2,3,4,5).
            var input = new[] { At(1, 1), At(2, 2), At(3, 3), At(4, 4), At(5, 5) };
            var sameTiles = new HashSet<Vector2> { new(1, 1), new(3, 3), new(5, 5) };

            var result = Helpers.SortSameLocationFirst(input, p => sameTiles.Contains(p.Key));

            result.Select(p => p.Key).Should().ContainInOrder(new[]
            {
                new Vector2(1, 1), new Vector2(3, 3), new Vector2(5, 5),
                new Vector2(2, 2), new Vector2(4, 4)
            });
        }

        [Fact]
        public void Mixed_AllCountsPreserved_NoLossNoDuplication()
        {
            var input = new[] { At(1, 1), At(2, 2), At(3, 3), At(4, 4) };
            var sameTiles = new HashSet<Vector2> { new(1, 1), new(2, 2) };

            var result = Helpers.SortSameLocationFirst(input, p => sameTiles.Contains(p.Key));

            result.Should().HaveCount(input.Length);
            result.Select(p => p.Key).Should().BeEquivalentTo(input.Select(p => p.Key));
        }
    }

    // Audit §2.10: the range-aware lookup that respects "local first" in global
    // modes. Same-location grabbers get the tile-distance filter; cross-location
    // grabbers passthrough as a fallback.
    public class GetGrabbersInRangeOrCrossLocation
    {
        [Fact]
        public void NegativeRange_ReturnsAllSameLocation_ThenAllCrossLocation_Unfiltered()
        {
            var input = new[]
            {
                At(0, 0),    // same, tile (0,0)
                At(50, 50),  // cross, far away
                At(100, 100) // same, far away (would fail any positive range)
            };
            var sameTiles = new HashSet<Vector2> { new(0, 0), new(100, 100) };

            var result = Helpers.GetGrabbersInRangeOrCrossLocation(
                tile: new Vector2(5, 5),
                grabbers: input,
                range: -1,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk,
                isSameLocation: p => sameTiles.Contains(p.Key));

            // Negative range = no filter, all same-location first, then cross.
            result.Select(p => p.Key).Should().ContainInOrder(new[]
            {
                new Vector2(0, 0),
                new Vector2(100, 100),
                new Vector2(50, 50)
            });
        }

        [Fact]
        public void RangeAppliedToSameLocationOnly_CrossLocationAlwaysPassesThrough()
        {
            // Search tile (10,10), range 5 (Walk). Cross-location grabbers' tile
            // coords are in another map's coordinate space, so they should pass
            // through unfiltered regardless of how far their numeric key is.
            //   Same (0,0): manhattan 20 -- EXCLUDED by range filter.
            //   Same (12,10): manhattan 2 -- INCLUDED.
            //   Cross (3,3): would pass range numerically but tile coords are
            //     meaningless across maps, so we expect "always include" semantics
            //     -- still INCLUDED, but as a cross-location passthrough.
            //   Cross (500,500): would fail range numerically -- still INCLUDED.
            var input = new[]
            {
                At(0, 0),       // same, OUT
                At(12, 10),     // same, IN
                At(3, 3),       // cross, would-pass-range
                At(500, 500),   // cross, would-fail-range
            };
            var sameTiles = new HashSet<Vector2> { new(0, 0), new(12, 10) };

            var result = Helpers.GetGrabbersInRangeOrCrossLocation(
                tile: new Vector2(10, 10),
                grabbers: input,
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk,
                isSameLocation: p => sameTiles.Contains(p.Key));

            // Expected: same-and-in-range first (just (12,10)), then both cross
            // entries in their original input order.
            result.Should().HaveCount(3);
            result[0].Key.Should().Be(new Vector2(12, 10));
            result[1].Key.Should().Be(new Vector2(3, 3));
            result[2].Key.Should().Be(new Vector2(500, 500));
        }

        [Fact]
        public void NoSameLocationGrabbers_StillReturnsAllCrossLocation()
        {
            var input = new[]
            {
                At(0, 0),
                At(1000, 1000),
            };

            var result = Helpers.GetGrabbersInRangeOrCrossLocation(
                tile: new Vector2(10, 10),
                grabbers: input,
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk,
                isSameLocation: _ => false);

            result.Should().HaveCount(2);
            result.Select(p => p.Key).Should().ContainInOrder(input.Select(p => p.Key));
        }

        [Fact]
        public void NoCrossLocationGrabbers_BehavesLikeNearbyObjectsToTile()
        {
            var input = new[]
            {
                At(10, 10),  // dist 0
                At(13, 10),  // dist 3
                At(20, 20),  // dist 20 (excluded)
            };

            var result = Helpers.GetGrabbersInRangeOrCrossLocation(
                tile: new Vector2(10, 10),
                grabbers: input,
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk,
                isSameLocation: _ => true);

            result.Select(p => p.Key).Should().BeEquivalentTo(new[]
            {
                new Vector2(10, 10),
                new Vector2(13, 10)
            });
        }

        [Fact]
        public void EmptyInput_ReturnsEmpty()
        {
            var result = Helpers.GetGrabbersInRangeOrCrossLocation(
                tile: Vector2.Zero,
                grabbers: System.Array.Empty<KeyValuePair<Vector2, Object>>(),
                range: 5,
                rangeMode: ModConfig.HarvestCropsRangeMode.Walk,
                isSameLocation: _ => true);

            result.Should().BeEmpty();
        }
    }
}
