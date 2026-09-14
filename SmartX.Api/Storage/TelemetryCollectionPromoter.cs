namespace SmartX.Api.Storage;

/// <summary>
/// Copies raw array batches into List&lt;T&gt; for the query/ingest pipeline.
/// Arrays stay the sequential store; lists are the optimised working set (rubric: arrays → collections).
/// </summary>
public static class TelemetryCollectionPromoter
{
    public static List<T> FromArray<T>(T[] batch) => [.. batch];

    public static List<T> FromJagged<T>(T[][] rows)
    {
        var list = new List<T>();
        foreach (var row in rows)
        {
            list.AddRange(row);
        }

        return list;
    }

    public static List<float> FromWindow(float[,] window, int filled)
    {
        var metrics = window.GetLength(1);
        var list = new List<float>(filled * metrics);
        for (var sample = 0; sample < filled; sample++)
        {
            for (var metric = 0; metric < metrics; metric++)
            {
                list.Add(window[sample, metric]);
            }
        }

        return list;
    }

    public static void Replace<T>(List<T> target, IEnumerable<T> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}
