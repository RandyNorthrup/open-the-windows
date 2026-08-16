namespace OpenTheWindows.Core.Catalog;

/// <summary>Maps registry <c>EditionID</c> strings to <see cref="WindowsEdition"/> families.</summary>
public static class WindowsEditions
{
    private static readonly Dictionary<string, WindowsEdition> ByEditionId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Core"] = WindowsEdition.Home,
        ["CoreSingleLanguage"] = WindowsEdition.Home,
        ["CoreCountrySpecific"] = WindowsEdition.Home,
        ["CoreN"] = WindowsEdition.Home,
        ["Professional"] = WindowsEdition.Pro,
        ["ProfessionalN"] = WindowsEdition.Pro,
        ["ProfessionalEducation"] = WindowsEdition.Pro,
        ["ProfessionalWorkstation"] = WindowsEdition.Pro,
        ["Enterprise"] = WindowsEdition.Enterprise,
        ["EnterpriseN"] = WindowsEdition.Enterprise,
        ["EnterpriseS"] = WindowsEdition.Enterprise,
        ["EnterpriseSN"] = WindowsEdition.Enterprise,
        ["IoTEnterprise"] = WindowsEdition.Enterprise,
        ["IoTEnterpriseS"] = WindowsEdition.Enterprise,
        ["Education"] = WindowsEdition.Education,
        ["EducationN"] = WindowsEdition.Education,
    };

    /// <summary>
    /// Returns the edition family for a registry <c>EditionID</c>, or
    /// <see langword="null"/> when the id is unknown (callers must not guess).
    /// </summary>
    public static WindowsEdition? FromEditionId(string? editionId)
        => editionId is not null && ByEditionId.TryGetValue(editionId, out WindowsEdition edition) ? edition : null;
}
