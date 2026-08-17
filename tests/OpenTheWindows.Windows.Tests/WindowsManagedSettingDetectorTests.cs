using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.TestSupport;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Detects Group Policy ownership by reading a Local GPO <c>Registry.pol</c>.
/// Uses a temporary policy root built with <see cref="PRegFixture"/> so the
/// tests are deterministic and need no elevation, plus one real-path check that
/// an unmanaged value reports <see cref="ManagedState.NotManaged"/>.
/// </summary>
public sealed class WindowsManagedSettingDetectorTests : IDisposable
{
    private const string PolicyKey = @"Software\Policies\Otw";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "otw-gp-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Detects_a_policy_managed_value()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [1, 0, 0, 0]));
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.GroupPolicy, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void Detects_a_managed_value_deletion()
    {
        WriteMachinePolicy((PolicyKey, "**Del.Gone", 4u, [0, 0, 0, 0]));
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.GroupPolicy, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Gone"));
    }

    [Fact]
    public void Value_or_key_not_in_policy_is_not_managed()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [1, 0, 0, 0]));
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Other"));
        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, @"Software\Other", "Flag"));
    }

    [Fact]
    public void Missing_policy_file_is_not_managed()
    {
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void Unmanaged_machine_reports_not_managed()
    {
        var detector = new WindowsManagedSettingDetector();

        ManagedState state = detector.Detect(
            RegistryHive.LocalMachine, @"Software\Policies\OpenTheWindows\NotReal", "NotReal");

        Assert.Equal(ManagedState.NotManaged, state);
    }

    private void WriteMachinePolicy(params (string Key, string Value, uint Type, byte[] Data)[] entries)
    {
        string machineDir = Path.Combine(_root, "Machine");
        Directory.CreateDirectory(machineDir);
        File.WriteAllBytes(Path.Combine(machineDir, "Registry.pol"), PRegFixture.Build(entries));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
