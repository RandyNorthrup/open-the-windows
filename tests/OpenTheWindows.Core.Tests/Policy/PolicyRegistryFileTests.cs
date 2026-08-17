using OpenTheWindows.Core.Policy;
using OpenTheWindows.TestSupport;

namespace OpenTheWindows.Core.Tests.Policy;

/// <summary>
/// Golden-fixture tests for the PReg (<c>Registry.pol</c>) parser, built with
/// <see cref="PRegFixture"/>.
/// </summary>
public sealed class PolicyRegistryFileTests
{
    [Fact]
    public void Parses_all_entries_with_fields()
    {
        byte[] bytes = PRegFixture.Build(
            (@"Software\Policies\Otw", "Flag", 4u, [1, 0, 0, 0]),
            (@"Software\Policies\Otw", "Gone", 4u, [0, 0, 0, 0]));

        IReadOnlyList<PolicyRegistryEntry> entries = PolicyRegistryFile.Parse(bytes);

        Assert.Equal(2, entries.Count);
        Assert.Equal(@"Software\Policies\Otw", entries[0].Key);
        Assert.Equal("Flag", entries[0].ValueName);
        Assert.Equal(4u, entries[0].Type);
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, entries[0].Data.ToArray());
        Assert.Equal("Gone", entries[1].ValueName);
    }

    [Fact]
    public void Header_only_file_has_no_entries()
    {
        byte[] bytes = PRegFixture.Build();

        Assert.Empty(PolicyRegistryFile.Parse(bytes));
    }

    [Fact]
    public void Bad_signature_throws()
    {
        byte[] bytes = [1, 2, 3, 4, 5, 6, 7, 8];

        Assert.Throws<FormatException>(() => PolicyRegistryFile.Parse(bytes));
    }

    [Fact]
    public void Truncated_buffer_throws()
    {
        byte[] full = PRegFixture.Build((@"Software\Policies\Otw", "Flag", 4u, [1, 0, 0, 0]));

        // Drop the closing bracket and part of the data.
        byte[] truncated = full[..^6];

        Assert.Throws<FormatException>(() => PolicyRegistryFile.Parse(truncated));
    }
}
