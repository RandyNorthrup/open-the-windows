using System.Diagnostics;
using System.Globalization;
using OpenTheWindows.Core.Catalog;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class CatalogPerformanceTests
{
    private const int EntryCount = 1000;

    // Measured ~0.65 s locally (including coverage instrumentation) for 1000
    // entries after the loader's pass/fail-first schema evaluation. The budget
    // is deliberately generous so slower CI runners never flake, while still
    // catching an order-of-magnitude regression (e.g. accidental O(n^2) loading,
    // or reverting the fast schema path, which doubled the time). The shipped
    // catalogue is ~30 entries, i.e. tens of milliseconds; 1000 is a stress case.
    // The one-time schema compile is excluded by a warm-up load.
    private const int BudgetMilliseconds = 3000;

    [Fact]
    public void Loading_a_thousand_entries_stays_within_budget()
    {
        string[] bodies = new string[EntryCount];
        for (int i = 0; i < EntryCount; i++)
        {
            bodies[i] = EntryJson(string.Create(CultureInfo.InvariantCulture, $"perf.entry.n{i}"));
        }

        string document = DocumentJson(bodies);
        CatalogSource[] sources = [new CatalogSource("perf.json", document, CatalogOrigin.BuiltIn)];

        // Warm up: compile the schema and JIT the load path once.
        _ = CatalogLoader.Load([new CatalogSource("warmup.json", DocumentJson(EntryJson("perf.warmup.entry")), CatalogOrigin.BuiltIn)]);

        var stopwatch = Stopwatch.StartNew();
        CatalogLoadResult result = CatalogLoader.Load(sources);
        stopwatch.Stop();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        Assert.Equal(EntryCount, result.Catalog!.Count);
        Assert.True(
            stopwatch.ElapsedMilliseconds <= BudgetMilliseconds,
            string.Create(CultureInfo.InvariantCulture,
                $"Loaded {EntryCount} entries in {stopwatch.ElapsedMilliseconds} ms (budget {BudgetMilliseconds} ms)."));
    }
}
