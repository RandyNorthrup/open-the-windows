using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Pre-flight stub returning a configurable report (passing by default).</summary>
public sealed class FakePreflight : IPreflight
{
    /// <summary>The report <see cref="Run"/> returns.</summary>
    public PreflightReport Report { get; set; } = new([]);

    /// <summary>The scope the engine passed to the most recent <see cref="Run"/> call.</summary>
    public Scope? LastScope { get; private set; }

    /// <inheritdoc />
    public PreflightReport Run(Scope scope)
    {
        LastScope = scope;
        return Report;
    }
}
