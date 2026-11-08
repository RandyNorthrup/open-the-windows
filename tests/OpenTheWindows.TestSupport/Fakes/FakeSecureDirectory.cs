using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Creates the requested folder and records it; refuses when <see cref="RefuseAll"/> is set.</summary>
public sealed class FakeSecureDirectory : ISecureDirectory
{
    private readonly List<string> _prepared = [];

    /// <summary>When set, every request throws as if the folder could not be secured.</summary>
    public bool RefuseAll { get; set; }

    /// <summary>The folders prepared, in order.</summary>
    public IReadOnlyList<string> Prepared => _prepared;

    /// <inheritdoc />
    public void EnsureSafeForWrite(string directory)
    {
        if (RefuseAll)
        {
            throw new UnauthorizedAccessException("The folder is not secured (test).");
        }

        Directory.CreateDirectory(directory);
        _prepared.Add(directory);
    }
}
