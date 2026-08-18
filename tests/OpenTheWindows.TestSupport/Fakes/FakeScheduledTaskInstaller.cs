using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// Records scheduled-task install/remove calls for assertions and performs no OS
/// action. <see cref="RemoveResult"/> configures what <see cref="Remove"/> returns.
/// </summary>
public sealed class FakeScheduledTaskInstaller : IScheduledTaskInstaller
{
    private readonly List<ScheduledTaskSpec> _installed = [];
    private readonly List<string> _removed = [];

    /// <summary>Every spec passed to <see cref="Install"/>, in order.</summary>
    public IReadOnlyList<ScheduledTaskSpec> Installed => _installed;

    /// <summary>Every task path passed to <see cref="Remove"/>, in order.</summary>
    public IReadOnlyList<string> Removed => _removed;

    /// <summary>What <see cref="Remove"/> returns (default: the task existed).</summary>
    public bool RemoveResult { get; set; } = true;

    /// <inheritdoc />
    public void Install(ScheduledTaskSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _installed.Add(spec);
    }

    /// <inheritdoc />
    public bool Remove(string taskPath)
    {
        _removed.Add(taskPath);
        return RemoveResult;
    }
}
