using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// Builds the startup-apps, app-access and persistence-watch services over scriptable
/// in-memory sources and a <see cref="FakeMachine"/>, so CLI and GUI tests drive them end to
/// end (startup changes go through the real apply engine and its journal).
/// </summary>
public static class FakeActivitySources
{
    /// <summary>The startup-apps service over <paramref name="source"/> and <paramref name="harness"/>'s machine and engine.</summary>
    public static StartupAppsService StartupApps(FakeStartupSource source, ApplyHarness harness, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(harness);
        return new StartupAppsService(source, harness.Machine, harness.Machine, harness.Engine, new FixedTimeProvider(at));
    }

    /// <summary>The app-access view over <paramref name="source"/>, with <paramref name="machine"/> resolving the signed-in user.</summary>
    public static CapabilityAccessService Access(FakeConsentStoreSource source, FakeMachine machine) => new(source, machine);

    /// <summary>The persistence watch over <paramref name="source"/> and <paramref name="store"/>.</summary>
    public static PersistenceWatchService Watch(FakePersistenceSource source, FakePersistenceSnapshotStore store, FakeMachine machine, DateTimeOffset at)
        => new(source, store, machine, new FixedTimeProvider(at));
}
