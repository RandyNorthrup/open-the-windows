using System.Diagnostics;
using System.Globalization;
using OpenTheWindows.Core.Catalog;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class CatalogPerformanceTests
{
    private const int BaselineCount = 100;
    private const int EntryCount = 1000;

    // A single wall-clock sample flakes when the gate machine is under load (a
    // background build, other test runners, coverage instrumentation). Each size
    // is timed several times and the fastest kept: the minimum reflects the
    // algorithmic cost and is immune to a transient scheduling stall, while a
    // genuine regression is slow on every run and still fails.
    private const int MeasuredRuns = 5;

    // Loading is O(n) in the entry count, so loading EntryCount entries should
    // take about EntryCount / BaselineCount (10x) the time of the baseline, plus
    // fixed overhead — nowhere near the ~100x an accidental O(n^2) load (or a
    // per-entry schema recompile) would show. Asserting the RATIO of two loads
    // measured back-to-back is immune to machine speed and to coverage
    // instrumentation, which scale both loads equally; an absolute wall-clock
    // budget is not, and flaked under coverage on shared CI runners. The bound is
    // generous (well above 10x linear, well below 100x quadratic) so a healthy
    // loader never flakes while a regression fails.
    private const double MaxRatio = 30.0;

    [Fact]
    public void Loading_scales_linearly_with_entry_count()
    {
        // Warm up: compile the schema and JIT the load path once, so neither
        // measurement pays the one-time cost.
        _ = CatalogLoader.Load([new CatalogSource("warmup.json", DocumentJson(EntryJson("perf.warmup.entry")), CatalogOrigin.BuiltIn)]);

        double baselineMs = BestLoadMilliseconds(BaselineCount, out _);
        double fullMs = BestLoadMilliseconds(EntryCount, out CatalogLoadResult fullResult);

        Assert.True(fullResult.IsValid, string.Join(Environment.NewLine, fullResult.Errors.Select(e => e.ToString())));
        Assert.Equal(EntryCount, fullResult.Catalog!.Count);

        double ratio = fullMs / baselineMs;
        Assert.True(
            ratio <= MaxRatio,
            string.Create(CultureInfo.InvariantCulture,
                $"Loading {EntryCount} entries took {ratio:F1}x the time of {BaselineCount} ({fullMs:F1} ms vs {baselineMs:F1} ms); linear is ~{EntryCount / BaselineCount}x, budget {MaxRatio}x."));
    }

    private static double BestLoadMilliseconds(int entryCount, out CatalogLoadResult result)
    {
        string[] bodies = new string[entryCount];
        for (int i = 0; i < entryCount; i++)
        {
            bodies[i] = EntryJson(string.Create(CultureInfo.InvariantCulture, $"perf.entry.n{i}"));
        }

        CatalogSource[] sources = [new CatalogSource("perf.json", DocumentJson(bodies), CatalogOrigin.BuiltIn)];

        double best = double.MaxValue;
        result = null!;
        for (int run = 0; run < MeasuredRuns; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            result = CatalogLoader.Load(sources);
            stopwatch.Stop();
            best = Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
        }

        return best;
    }
}
