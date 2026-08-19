using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Tests.Audit;

/// <summary>Shared helper: the validated built-in catalogue for the audit tests.</summary>
internal static class AuditTestCatalog
{
    public static TweakCatalog LoadBuiltIn()
    {
        CatalogLoadResult result = CatalogLoader.LoadBuiltIn();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        return result.Catalog!;
    }
}
