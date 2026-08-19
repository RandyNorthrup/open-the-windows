using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// A single configurable stand-in for every read-only reader the scan engine
/// needs. Each callback defaults to "absent / not managed"; a test overrides
/// only the reader it exercises. Interface methods are implemented explicitly
/// because several are <c>Read(string)</c> with different return types.
/// </summary>
public sealed class FakeReaders :
    IRegistryReader, IServiceReader, IScheduledTaskReader, IAppxReader,
    IOptionalFeatureReader, IDefenderPreferenceReader, IPowerSettingReader,
    ISecurityPolicyReader, IInteractiveUserResolver, IManagedSettingDetector
{
    public Func<RegistryHive, string?, string, string, RegistryValueSnapshot> OnRegistry { get; set; }
        = (_, _, _, _) => new RegistryValueSnapshot(false, null, null);

    public Func<string, ServiceSnapshot?> OnService { get; set; } = _ => null;

    public Func<string, ScheduledTaskSnapshot?> OnTask { get; set; } = _ => null;

    public Func<string, string?, AppxSnapshot> OnAppx { get; set; }
        = (_, _) => new AppxSnapshot(false, false, false, false);

    public Func<string, bool?> OnFeature { get; set; } = _ => null;

    public Func<string, JsonElement?> OnDefender { get; set; } = _ => null;

    public Func<Guid, Guid, (uint Ac, uint Dc)?> OnPower { get; set; } = (_, _) => null;

    public Func<SecurityPolicySection, string, string?> OnSecurityPolicy { get; set; } = (_, _) => null;

    public Func<InteractiveUser?> OnInteractiveUser { get; set; } = () => null;

    public Func<RegistryHive, string, string, ManagedState> OnManaged { get; set; }
        = (_, _, _) => ManagedState.NotManaged;

    RegistryValueSnapshot IRegistryReader.Read(RegistryHive hive, string? userSid, string path, string name)
        => OnRegistry(hive, userSid, path, name);

    ServiceSnapshot? IServiceReader.Read(string name) => OnService(name);

    ScheduledTaskSnapshot? IScheduledTaskReader.Read(string path) => OnTask(path);

    AppxSnapshot IAppxReader.Read(string packageFamilyName, string? userSid) => OnAppx(packageFamilyName, userSid);

    bool? IOptionalFeatureReader.IsEnabled(string featureName) => OnFeature(featureName);

    JsonElement? IDefenderPreferenceReader.Read(string propertyName) => OnDefender(propertyName);

    (uint Ac, uint Dc)? IPowerSettingReader.Read(Guid subgroup, Guid setting) => OnPower(subgroup, setting);

    string? ISecurityPolicyReader.Read(SecurityPolicySection section, string setting) => OnSecurityPolicy(section, setting);

    InteractiveUser? IInteractiveUserResolver.Resolve() => OnInteractiveUser();

    ManagedState IManagedSettingDetector.Detect(RegistryHive hive, string path, string name) => OnManaged(hive, path, name);
}
