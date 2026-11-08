using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Reports <see cref="Weakness"/> for every executable (null means safe) and records what it checked.</summary>
public sealed class FakeExecutableLocationGuard : IExecutableLocationGuard
{
    private readonly List<string> _checked = [];

    /// <summary>The weakness to report, or <see langword="null"/> for a safe location.</summary>
    public string? Weakness { get; set; }

    /// <summary>The paths checked, in order.</summary>
    public IReadOnlyList<string> Checked => _checked;

    /// <inheritdoc />
    public string? FindWeakness(string executablePath)
    {
        _checked.Add(executablePath);
        return Weakness;
    }
}
