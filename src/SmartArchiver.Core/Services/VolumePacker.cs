using System.Collections;

namespace SmartArchiver.Core.Services;

public static class VolumePacker
{
    public readonly record struct Item(int Weight, int Value);

    public static int[] Select(IReadOnlyList<Item> items, int capacity, bool greedy)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (items.Any(x => x.Weight <= 0 || x.Value <= 0))
            throw new ArgumentException("Weights and values must be positive.", nameof(items));

        if (greedy)
        {
            var selected = new List<int>();
            foreach (int i in Enumerable.Range(0, items.Count)
                         .OrderByDescending(i => (double)items[i].Value / items[i].Weight))
                if (items[i].Weight <= capacity)
                {
                    selected.Add(i);
                    capacity -= items[i].Weight;
                }
            return selected.ToArray();
        }

        if (items.Sum(x => (long)x.Weight) <= capacity)
            return Enumerable.Range(0, items.Count).ToArray();
        // Descending capacities keep each item usable once. Decisions are one bit per state.
        var values = new long[capacity + 1];
        var decisions = new BitArray[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var (weight, value) = items[i];
            var take = decisions[i] = new BitArray(capacity + 1);
            for (int w = capacity; w >= weight; w--)
            {
                long candidate = values[w - weight] + value;
                if (candidate <= values[w]) continue;
                values[w] = candidate;
                take[w] = true;
            }
        }
        var result = new List<int>();
        for (int i = items.Count - 1; i >= 0; i--)
            if (decisions[i][capacity])
            {
                result.Add(i);
                capacity -= items[i].Weight;
            }
        result.Reverse();
        return result.ToArray();
    }
}
