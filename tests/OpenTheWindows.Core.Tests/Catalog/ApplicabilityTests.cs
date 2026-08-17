using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class ApplicabilityTests
{
    private static OperatingSystemFacts Facts(int build = 26100, string architecture = "x64", string editionId = "Professional")
        => new("Windows 11 Pro", editionId, "24H2", build, 0, architecture, "Client");

    [Fact]
    public void Any_matches_every_machine()
    {
        Assert.True(Applicability.Any.Matches(Facts()));
        Assert.True(Applicability.Any.Matches(Facts(build: 22000, architecture: "arm64", editionId: "Core")));
    }

    [Theory]
    [InlineData(21999, false)]
    [InlineData(26000, true)]
    [InlineData(27000, true)]
    public void Min_build_is_an_inclusive_lower_bound(int build, bool expected)
    {
        var applicability = new Applicability([], 26000, null, []);

        Assert.Equal(expected, applicability.Matches(Facts(build: build)));
    }

    [Theory]
    [InlineData(26100, true)]
    [InlineData(26101, false)]
    public void Max_build_is_an_inclusive_upper_bound(int build, bool expected)
    {
        var applicability = new Applicability([], null, 26100, []);

        Assert.Equal(expected, applicability.Matches(Facts(build: build)));
    }

    [Theory]
    [InlineData("arm64", true)]
    [InlineData("ARM64", true)]     // case-insensitive
    [InlineData("x64", false)]
    public void Architecture_filter_matches_case_insensitively(string architecture, bool expected)
    {
        var applicability = new Applicability([], null, null, ["arm64"]);

        Assert.Equal(expected, applicability.Matches(Facts(architecture: architecture)));
    }

    [Theory]
    [InlineData("Professional", true)]
    [InlineData("Core", false)]         // Home family
    [InlineData("Frobozz", false)]      // unknown edition never matches a restricted entry
    public void Edition_filter_uses_the_edition_family(string editionId, bool expected)
    {
        var applicability = new Applicability([WindowsEdition.Pro], null, null, []);

        Assert.Equal(expected, applicability.Matches(Facts(editionId: editionId)));
    }

    [Fact]
    public void Empty_edition_list_matches_even_unknown_editions()
    {
        Assert.True(Applicability.Any.Matches(Facts(editionId: "Frobozz")));
    }
}
