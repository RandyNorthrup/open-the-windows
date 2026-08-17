using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class CatalogCommandTests
{
    private const string KnownId = "privacy.advertising-id.disable";

    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(Func<string?, CatalogLoadResult>? loadCatalog = null)
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        Func<string?, CatalogLoadResult> load = loadCatalog ?? (_ => CatalogLoader.LoadBuiltIn());
        return CliTestHost.Host(TestCli.Services(doctor, load));
    }

    private static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
        => CliTestHost.Invoke(root, stdout, stderr, args);

    [Fact]
    public void List_prints_a_table_with_a_count()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "list");

        Assert.Equal(ExitCodes.Success, exit);
        string text = stdout.ToString();
        Assert.Contains("CATEGORY", text, StringComparison.Ordinal);
        Assert.Contains("entries.", text, StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void List_json_emits_an_array_of_entries()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "list", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 20);
    }

    [Fact]
    public void List_filters_by_category_and_level()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "list", "--category", "Privacy", "--level", "Basic", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        int length = doc.RootElement.GetArrayLength();
        Assert.True(length > 0);
        for (int i = 0; i < length; i++)
        {
            JsonElement entry = doc.RootElement[i];
            Assert.Equal("Privacy", entry.GetProperty("category").GetString());
            Assert.Equal("Basic", entry.GetProperty("level").GetString());
        }
    }

    [Fact]
    public void Show_prints_the_entry_as_text()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "show", KnownId);

        Assert.Equal(ExitCodes.Success, exit);
        string text = stdout.ToString();
        Assert.Contains(KnownId, text, StringComparison.Ordinal);
        Assert.Contains("Sources:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_json_emits_the_entry_object()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "show", KnownId, "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(KnownId, doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void Show_unknown_id_exits_four()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "show", "privacy.does-not.exist");

        Assert.Equal(ExitCodes.InvalidInput, exit);
        Assert.Contains("No catalogue entry", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Show_malformed_id_exits_four()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "show", "Not A Valid Id");

        Assert.Equal(ExitCodes.InvalidInput, exit);
        Assert.Contains("not a valid tweak id", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_valid_catalogue_exits_zero()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "validate");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("Catalogue valid:", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_json_emits_a_report()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "catalog", "validate", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("entryCount").GetInt32() >= 20);
    }

    [Fact]
    public void Validate_invalid_catalogue_exits_four_and_prints_issues()
    {
        static CatalogLoadResult Loader(string? directory) => directory is null
            ? CatalogLoader.LoadBuiltIn()
            : new CatalogLoadResult(null,
                [new CatalogIssue(CatalogIssueSeverity.Error, "broken.json", "/entries/0", "schema", "intentionally broken")]);

        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build(Loader);

        int exit = Invoke(root, stdout, stderr, "catalog", "validate", "--catalog-dir", "any-directory");

        Assert.Equal(ExitCodes.InvalidInput, exit);
        string text = stdout.ToString();
        Assert.Contains("Catalogue INVALID:", text, StringComparison.Ordinal);
        Assert.Contains("schema", text, StringComparison.Ordinal);
    }
}
