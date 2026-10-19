namespace OpenCalendar.Core.Agenda;

/// <summary>Column placement of a timed item inside a day column.</summary>
public readonly record struct LayoutSlot(int Column, int ColumnCount);

/// <summary>
/// Side-by-side layout for overlapping timed items (like other calendars): overlapping items form a cluster,
/// each item gets the first free column, and every item in a cluster shares the cluster's column count.
/// </summary>
public static class DayLayout
{
    public static IReadOnlyList<LayoutSlot> Arrange(IReadOnlyList<(double Start, double End)> items)
    {
        var slots = new LayoutSlot[items.Count];
        var order = Enumerable.Range(0, items.Count)
            .OrderBy(i => items[i].Start)
            .ThenByDescending(i => items[i].End - items[i].Start)
            .ToList();

        var cluster = new List<int>();
        var columnEnds = new List<double>();
        var clusterEnd = double.MinValue;

        void CloseCluster()
        {
            foreach (var index in cluster) slots[index] = slots[index] with { ColumnCount = columnEnds.Count };
            cluster.Clear();
            columnEnds.Clear();
        }

        foreach (var index in order)
        {
            var (start, end) = items[index];
            end = Math.Max(end, start + 1);

            if (cluster.Count > 0 && start >= clusterEnd) CloseCluster();

            var column = columnEnds.FindIndex(columnEnd => columnEnd <= start);
            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(end);
            }
            else
            {
                columnEnds[column] = end;
            }

            slots[index] = new LayoutSlot(column, 1);
            cluster.Add(index);
            clusterEnd = cluster.Count == 1 ? end : Math.Max(clusterEnd, end);
        }

        CloseCluster();
        return slots;
    }
}
