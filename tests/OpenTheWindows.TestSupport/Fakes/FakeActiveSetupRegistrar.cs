using System.Globalization;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// In-memory Active Setup registrar for command tests: records every registered
/// component (bumping a per-key version) and every <see cref="RemoveAll"/> call,
/// without touching the registry. Set <see cref="SimulateOobe"/> to exercise the
/// OOBE skip.
/// </summary>
public sealed class FakeActiveSetupRegistrar : IActiveSetupRegistrar
{
    private readonly Dictionary<string, int> _versions = new(StringComparer.Ordinal);
    private readonly List<ActiveSetupComponent> _registered = [];

    /// <summary>Every component passed to <see cref="Register"/>, in call order.</summary>
    public IReadOnlyList<ActiveSetupComponent> Registered => _registered;

    /// <summary>When true, <see cref="Register"/> reports the OOBE skip and records nothing.</summary>
    public bool SimulateOobe { get; set; }

    /// <summary>How many times <see cref="RemoveAll"/> was called.</summary>
    public int RemoveAllCalls { get; private set; }

    /// <inheritdoc />
    public ActiveSetupResult Register(ActiveSetupComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (SimulateOobe)
        {
            return new ActiveSetupResult(ActiveSetupOutcome.SkippedDuringOobe, null, component.ComponentKey);
        }

        int version = _versions.TryGetValue(component.ComponentKey, out int current) ? current + 1 : 1;
        _versions[component.ComponentKey] = version;
        _registered.Add(component);
        return new ActiveSetupResult(
            ActiveSetupOutcome.Registered, version.ToString(CultureInfo.InvariantCulture), component.ComponentKey);
    }

    /// <inheritdoc />
    public int RemoveAll()
    {
        RemoveAllCalls++;
        int count = _versions.Count;
        _versions.Clear();
        return count;
    }
}
