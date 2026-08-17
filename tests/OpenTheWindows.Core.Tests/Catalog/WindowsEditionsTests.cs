using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class WindowsEditionsTests
{
    [Theory]
    [InlineData("Core", WindowsEdition.Home)]
    [InlineData("CoreSingleLanguage", WindowsEdition.Home)]
    [InlineData("CoreN", WindowsEdition.Home)]
    [InlineData("Professional", WindowsEdition.Pro)]
    [InlineData("ProfessionalN", WindowsEdition.Pro)]
    [InlineData("ProfessionalWorkstation", WindowsEdition.Pro)]
    [InlineData("ProfessionalEducation", WindowsEdition.Pro)]
    [InlineData("Enterprise", WindowsEdition.Enterprise)]
    [InlineData("EnterpriseS", WindowsEdition.Enterprise)]
    [InlineData("IoTEnterprise", WindowsEdition.Enterprise)]
    [InlineData("Education", WindowsEdition.Education)]
    [InlineData("professional", WindowsEdition.Pro)]     // case-insensitive
    public void Maps_known_edition_ids_to_families(string editionId, WindowsEdition expected)
    {
        Assert.Equal(expected, WindowsEditions.FromEditionId(editionId));
    }

    [Theory]
    [InlineData("ServerStandard")]
    [InlineData("Team")]                                 // Surface Hub
    [InlineData("")]
    [InlineData("nonsense-edition")]
    public void Unknown_edition_ids_map_to_null(string editionId)
    {
        Assert.Null(WindowsEditions.FromEditionId(editionId));
    }

    [Fact]
    public void Null_edition_id_maps_to_null()
    {
        Assert.Null(WindowsEditions.FromEditionId(null));
    }
}
