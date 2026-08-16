using System.Reflection;

namespace OpenTheWindows.Core.Tests;

/// <summary>
/// The exit-code table is a public contract consumed by scripts; pin the values
/// so a refactor cannot silently renumber them.
/// </summary>
public sealed class ExitCodesTests
{
    [Fact]
    public void Values_are_pinned()
    {
        Assert.Equal(0, ExitCodes.Success);
        Assert.Equal(1, ExitCodes.Drift);
        Assert.Equal(2, ExitCodes.Error);
        Assert.Equal(3, ExitCodes.UnsupportedPlatform);
        Assert.Equal(4, ExitCodes.InvalidInput);
        Assert.Equal(5, ExitCodes.ElevationRequired);
        Assert.Equal(3010, ExitCodes.RebootRequired);
    }

    [Fact]
    public void Values_are_unique()
    {
        int[] values = typeof(ExitCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int))
            .Select(f => (int)f.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.Equal(7, values.Length);
    }
}
