using System.Globalization;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Records hive load/unload calls; the in-memory machine accepts writes to any SID, so loading is a no-op here.</summary>
public sealed class FakeUserHiveLoader : IUserHiveLoader
{
    private readonly List<string> _loaded = [];
    private readonly List<string> _unloaded = [];

    /// <summary>Mount keys (SIDs) that were loaded, in order.</summary>
    public IReadOnlyList<string> Loaded => _loaded;

    /// <summary>Mount keys (SIDs) that were unloaded, in order.</summary>
    public IReadOnlyList<string> Unloaded => _unloaded;

    /// <summary>When set, <see cref="Unload"/> throws — to assert the engine audits an unload failure without failing the run.</summary>
    public bool FailUnload { get; init; }

    /// <inheritdoc />
    public void Load(string mountKey, string hivePath)
    {
        if (FailLoadFor.Contains(mountKey))
        {
            throw new IOException(string.Create(CultureInfo.InvariantCulture, $"Load failed for {mountKey}."));
        }

        _loaded.Add(mountKey);
    }

    /// <summary>SIDs whose hive fails to load (a missing or locked NTUSER.DAT).</summary>
    public ISet<string> FailLoadFor { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Unload(string mountKey)
    {
        _unloaded.Add(mountKey);
        if (FailUnload)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Unload failed for {mountKey}."));
        }
    }
}
