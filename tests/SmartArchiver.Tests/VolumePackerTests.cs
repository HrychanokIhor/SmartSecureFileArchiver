using SmartArchiver.Core.Services;

namespace SmartArchiver.Tests;

public class VolumePackerTests
{
    [Fact]
    public void ExactPackerFindsCombinationGreedyMisses()
    {
        VolumePacker.Item[] items = [new(6, 12), new(5, 9), new(5, 9)];
        Assert.Equal(new[] { 0 }, VolumePacker.Select(items, 10, greedy: true));
        Assert.Equal(new[] { 1, 2 }, VolumePacker.Select(items, 10, greedy: false));
    }

    [Fact]
    public void ExactPackerMatchesExhaustiveSearchOnSmallInstances()
    {
        var random = new Random(42);
        for (int i = 0; i < 40; i++)
        {
            var items = Enumerable.Range(0, 8)
                .Select(_ => new VolumePacker.Item(random.Next(1, 10), random.Next(1, 10))).ToArray();
            int capacity = random.Next(0, 25);
            int best = 0;
            for (int mask = 0; mask < 1 << items.Length; mask++)
            {
                var subset = items.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
                if (subset.Sum(x => x.Weight) <= capacity) best = Math.Max(best, subset.Sum(x => x.Value));
            }
            int[] chosen = VolumePacker.Select(items, capacity, greedy: false);
            Assert.Equal(chosen.Length, chosen.Distinct().Count());
            Assert.True(chosen.Sum(j => items[j].Weight) <= capacity);
            Assert.Equal(best, chosen.Sum(j => items[j].Value));
        }
    }
}
