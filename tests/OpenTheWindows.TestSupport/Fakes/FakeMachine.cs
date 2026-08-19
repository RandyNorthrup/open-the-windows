using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// An in-memory machine implementing every state reader and writer over shared
/// mutable dictionaries, so an apply run's writes are observable by the verify
/// reads and by revert. Supports fault injection (throw on the Nth write, or
/// silently drop a registry value) to exercise rollback.
/// </summary>
public sealed class FakeMachine :
    IRegistryReader, IServiceReader, IScheduledTaskReader, IAppxReader,
    IOptionalFeatureReader, IDefenderPreferenceReader, IPowerSettingReader,
    IManagedSettingDetector, IInteractiveUserResolver, ISecurityPolicyReader,
    IRegistryWriter, IServiceWriter, IScheduledTaskWriter, IAppxWriter,
    IOptionalFeatureWriter, IDefenderPreferenceWriter, IPowerSettingWriter, ICommandRunner,
    ISecurityPolicyWriter
{
    private readonly Dictionary<string, (RegistryValueType Type, JsonElement Data)> _registry = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ServiceSnapshot> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScheduledTaskSnapshot> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppxSnapshot> _appx = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _features = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JsonElement> _defender = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (uint Ac, uint Dc)> _power = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _securityPolicy = new(StringComparer.Ordinal);
    private readonly HashSet<string> _managed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The commands the run invoked, in order.</summary>
    public IList<(string Executable, IReadOnlyList<string> Arguments)> Commands { get; } = [];

    /// <summary>Exit code the command runner returns.</summary>
    public int CommandExitCode { get; set; }

    /// <summary>The interactive user the resolver returns.</summary>
    public InteractiveUser? User { get; set; }

    /// <summary>Number of mutating writes performed so far.</summary>
    public int WriteCount { get; private set; }

    /// <summary>When set, the write whose 1-based number equals this value throws.</summary>
    public int? ThrowOnWrite { get; set; }

    /// <summary>When set, every write whose 1-based number is at least this value throws (to fail rollback too).</summary>
    public int? ThrowFromWrite { get; set; }

    /// <summary>Registry value names whose writes are silently dropped (to force a verify mismatch).</summary>
    public ISet<string> DropRegistryValues { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>When set, both the journal store and this machine append their operations here for ordering assertions.</summary>
    public IList<string>? Trace { get; init; }

    // ---- Seed helpers -----------------------------------------------------

    /// <summary>Seeds a registry value.</summary>
    public void SetRegistry(RegistryHive hive, string? sid, string path, string name, RegistryValueType type, JsonElement data)
        => _registry[RegistryKey(hive, sid, path, name)] = (type, data);

    /// <summary>Seeds a service.</summary>
    public void SetService(ServiceSnapshot snapshot) => _services[snapshot.Name] = snapshot;

    /// <summary>Seeds a scheduled task.</summary>
    public void SetTask(ScheduledTaskSnapshot snapshot) => _tasks[snapshot.Path] = snapshot;

    /// <summary>Seeds an optional feature state.</summary>
    public void SetFeature(string name, bool enabled) => _features[name] = enabled;

    /// <summary>Seeds a Defender preference value.</summary>
    public void SetDefender(string property, JsonElement value) => _defender[property] = value;

    /// <summary>Seeds a power setting value.</summary>
    public void SetPower(Guid subgroup, Guid setting, uint ac, uint dc) => _power[PowerKey(subgroup, setting)] = (ac, dc);

    /// <summary>Seeds a security-policy setting value.</summary>
    public void SetSecurityPolicy(SecurityPolicySection section, string setting, string value)
        => _securityPolicy[SecurityPolicyKey(section, setting)] = value;

    /// <summary>Seeds an Appx package state.</summary>
    public void SetAppx(string packageFamilyName, AppxSnapshot snapshot) => _appx[packageFamilyName] = snapshot;

    /// <summary>Marks a registry target as managed by Group Policy.</summary>
    public void SetManaged(RegistryHive hive, string path, string name) => _managed.Add(ManagedKey(hive, path, name));

    // ---- Readers ----------------------------------------------------------

    RegistryValueSnapshot IRegistryReader.Read(RegistryHive hive, string? userSid, string path, string name)
        => _registry.TryGetValue(RegistryKey(hive, userSid, path, name), out (RegistryValueType Type, JsonElement Data) value)
            ? new RegistryValueSnapshot(true, value.Type, value.Data)
            : new RegistryValueSnapshot(false, null, null);

    ServiceSnapshot? IServiceReader.Read(string name) => _services.GetValueOrDefault(name);

    ScheduledTaskSnapshot? IScheduledTaskReader.Read(string path) => _tasks.GetValueOrDefault(path);

    AppxSnapshot IAppxReader.Read(string packageFamilyName, string? userSid)
        => _appx.GetValueOrDefault(packageFamilyName, new AppxSnapshot(false, false, false, false));

    bool? IOptionalFeatureReader.IsEnabled(string featureName)
        => _features.TryGetValue(featureName, out bool enabled) ? enabled : null;

    JsonElement? IDefenderPreferenceReader.Read(string propertyName)
        => _defender.TryGetValue(propertyName, out JsonElement value) ? value : null;

    (uint Ac, uint Dc)? IPowerSettingReader.Read(Guid subgroup, Guid setting)
        => _power.TryGetValue(PowerKey(subgroup, setting), out (uint Ac, uint Dc) value) ? value : null;

    ManagedState IManagedSettingDetector.Detect(RegistryHive hive, string path, string name)
        => _managed.Contains(ManagedKey(hive, path, name)) ? ManagedState.GroupPolicy : ManagedState.NotManaged;

    InteractiveUser? IInteractiveUserResolver.Resolve() => User;

    string? ISecurityPolicyReader.Read(SecurityPolicySection section, string setting)
        => _securityPolicy.GetValueOrDefault(SecurityPolicyKey(section, setting));

    // ---- Writers ----------------------------------------------------------

    void IRegistryWriter.Write(RegistryHive hive, string? userSid, string path, string name, RegistryValueType type, JsonElement data, RegistryValueKind kind)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"registry:{hive}:{path}:{name}"));
        if (!DropRegistryValues.Contains(name))
        {
            _registry[RegistryKey(hive, userSid, path, name)] = (type, data.Clone());
        }
    }

    void IRegistryWriter.Delete(RegistryHive hive, string? userSid, string path, string name, RegistryValueKind kind)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"registry-delete:{hive}:{path}:{name}"));
        _registry.Remove(RegistryKey(hive, userSid, path, name));
    }

    void IServiceWriter.SetStartType(string name, ServiceStartType startType)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"service-start:{name}"));
        if (_services.TryGetValue(name, out ServiceSnapshot? snapshot))
        {
            _services[name] = snapshot with { StartType = startType };
        }
    }

    void IServiceWriter.StopService(string name, TimeSpan timeout)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"service-stop:{name}"));
        if (_services.TryGetValue(name, out ServiceSnapshot? snapshot))
        {
            _services[name] = snapshot with { IsRunning = false };
        }
    }

    void IScheduledTaskWriter.SetEnabled(string path, bool enabled)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"task:{path}"));
        if (_tasks.TryGetValue(path, out ScheduledTaskSnapshot? snapshot))
        {
            _tasks[path] = snapshot with { Enabled = enabled };
        }
    }

    void IAppxWriter.Remove(string packageFamilyName, string? userSid, AppxRemovalMode mode)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"appx-remove:{packageFamilyName}"));
        _appx[packageFamilyName] = mode == AppxRemovalMode.CurrentUser
            ? new AppxSnapshot(false, false, false, false)
            : new AppxSnapshot(false, false, false, true);
    }

    void IAppxWriter.Reprovision(string packageFamilyName)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"appx-reprovision:{packageFamilyName}"));
        _appx[packageFamilyName] = new AppxSnapshot(false, false, true, false);
    }

    void IOptionalFeatureWriter.SetEnabled(string name, bool enabled)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"feature:{name}"));
        _features[name] = enabled;
    }

    void IDefenderPreferenceWriter.SetPreference(string preference, JsonElement value)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"defender:{preference}"));
        _defender[preference] = value.Clone();
    }

    void IPowerSettingWriter.SetValues(Guid subgroup, Guid setting, uint ac, uint dc)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"power:{subgroup}:{setting}"));
        _power[PowerKey(subgroup, setting)] = (ac, dc);
    }

    CommandResult ICommandRunner.Run(string executable, IReadOnlyList<string> args, TimeSpan timeout)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"command:{executable}"));
        Commands.Add((executable, args));
        return new CommandResult(CommandExitCode, string.Empty, string.Empty, TimedOut: false);
    }

    void ISecurityPolicyWriter.Configure(SecurityPolicySection section, string setting, string value)
    {
        Mutate(string.Create(CultureInfo.InvariantCulture, $"securitypolicy:{section}:{setting}"));
        _securityPolicy[SecurityPolicyKey(section, setting)] = value;
    }

    private void Mutate(string what)
    {
        WriteCount++;
        Trace?.Add(string.Create(CultureInfo.InvariantCulture, $"write:{what}"));
        if (WriteCount == ThrowOnWrite || WriteCount >= ThrowFromWrite)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Injected failure on write {WriteCount}."));
        }
    }

    private static string RegistryKey(RegistryHive hive, string? sid, string path, string name)
        => string.Create(CultureInfo.InvariantCulture, $"{hive}|{sid ?? string.Empty}|{path}|{name}");

    private static string ManagedKey(RegistryHive hive, string path, string name)
        => string.Create(CultureInfo.InvariantCulture, $"{hive}|{path}|{name}");

    private static string PowerKey(Guid subgroup, Guid setting)
        => string.Create(CultureInfo.InvariantCulture, $"{subgroup}|{setting}");

    private static string SecurityPolicyKey(SecurityPolicySection section, string setting)
        => string.Create(CultureInfo.InvariantCulture, $"{section}|{setting}");
}
