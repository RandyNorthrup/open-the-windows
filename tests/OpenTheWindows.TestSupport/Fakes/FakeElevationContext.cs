using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Configurable elevation facts for unit tests.</summary>
public sealed class FakeElevationContext : IElevationContext
{
    public FakeElevationContext(bool isElevated, string processUserName = @"TEST-PC\tester")
    {
        IsElevated = isElevated;
        ProcessUserName = processUserName;
    }

    public bool IsElevated { get; set; }

    public string ProcessUserName { get; set; }
}
