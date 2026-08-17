namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Windows 11 edition families relevant to policy applicability. Many policies
/// are honoured only on Pro and above, or only on Enterprise/Education.
/// </summary>
public enum WindowsEdition
{
    /// <summary>Home ("Core", "CoreSingleLanguage", "CoreCountrySpecific").</summary>
    Home = 0,

    /// <summary>Pro, Pro Education, Pro for Workstations.</summary>
    Pro = 1,

    /// <summary>Enterprise, Enterprise LTSC ("EnterpriseS"), IoT Enterprise.</summary>
    Enterprise = 2,

    /// <summary>Education.</summary>
    Education = 3,
}
