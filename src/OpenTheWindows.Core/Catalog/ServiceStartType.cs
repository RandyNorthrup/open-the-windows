namespace OpenTheWindows.Core.Catalog;

/// <summary>Service start types the catalogue may set. "Disabled" is only permitted for services on the allow-list enforced by the validator.</summary>
public enum ServiceStartType
{
    Automatic = 0,
    AutomaticDelayed = 1,
    Manual = 2,
    Disabled = 3,
}
