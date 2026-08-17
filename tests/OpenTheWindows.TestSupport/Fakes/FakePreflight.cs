using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Pre-flight stub returning a configurable report (passing by default).</summary>
public sealed class FakePreflight : IPreflight
{
    /// <summary>The report <see cref="Run"/> returns.</summary>
    public PreflightReport Report { get; set; } = new([]);

    /// <inheritdoc />
    public PreflightReport Run() => Report;
}
