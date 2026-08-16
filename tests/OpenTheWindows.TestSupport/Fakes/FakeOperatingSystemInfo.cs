using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Configurable OS facts for unit tests.</summary>
public sealed class FakeOperatingSystemInfo : IOperatingSystemInfo
{
    public FakeOperatingSystemInfo(OperatingSystemFacts facts)
    {
        Current = facts;
    }

    /// <summary>The facts returned by <see cref="GetFacts"/>; mutate to simulate change between calls.</summary>
    public OperatingSystemFacts Current { get; set; }

    public OperatingSystemFacts GetFacts() => Current;

    public static OperatingSystemFacts Windows11Pro24H2()
        => new("Windows 11 Pro", "Professional", "24H2", 26100, 4351, "x64", "Client");

    public static OperatingSystemFacts Windows10Pro22H2()
        => new("Windows 10 Pro", "Professional", "22H2", 19045, 5011, "x64", "Client");

    public static OperatingSystemFacts WindowsServer2025()
        => new("Windows Server 2025 Standard", "ServerStandard", "24H2", 26100, 4351, "x64", "Server");
}
