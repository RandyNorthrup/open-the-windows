using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// <see cref="IPersistenceSource"/> over the registry, Task Scheduler and the Startup folders:
/// every service and driver registration (type, start, binary, service DLL), every scheduled
/// task (enabled state and actions), the Run and RunOnce values of HKLM, 32-bit HKLM and the
/// signed-in user, and the Startup-folder files. Per-session user-service instances are left
/// out (their names change at every sign-in). A folder or task this process cannot read is
/// skipped, not fatal.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPersistenceSource : IPersistenceSource
{
    private const string ServicesPath = @"SYSTEM\CurrentControlSet\Services";
    private const string RunOncePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunOnce32Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string MachineLabel = "HKLM";
    private const string UserLabel = "HKCU";
    private const int UserServiceInstanceFlag = 0x80;
    private const string TypeValue = "Type";
    private const string StartValue = "Start";
    private const string ImagePathValue = "ImagePath";
    private const string ParametersKey = "Parameters";
    private const string ServiceDllValue = "ServiceDll";

    private readonly IStartupSource _startup;

    /// <summary>Creates the source over the real Startup folders.</summary>
    public WindowsPersistenceSource()
        : this(new WindowsStartupSource())
    {
    }

    /// <summary>Creates the source over a given Startup source (tests).</summary>
    internal WindowsPersistenceSource(IStartupSource startup) => _startup = startup;

    /// <inheritdoc />
    public IReadOnlyList<PersistenceItem> Capture(string? userSid)
        => [.. Services(), .. Tasks(), .. RunValues(userSid), .. StartupFiles(userSid)];

    /// <summary>The service and driver registrations, keyed by service name.</summary>
    internal static IEnumerable<PersistenceItem> Services()
    {
        using RegistryKey? services = RegistryEnumeration.Open(null, ServicesPath);
        if (services is null)
        {
            yield break;
        }

        foreach (string name in RegistryEnumeration.SubKeys(services))
        {
            using RegistryKey? service = RegistryEnumeration.TryOpenSubKey(services, name);
            if (service?.GetValue(TypeValue) is not int type || (type & UserServiceInstanceFlag) != 0)
            {
                continue;
            }

            using RegistryKey? parameters = RegistryEnumeration.TryOpenSubKey(service, ParametersKey);
            string image = service.GetValue(ImagePathValue, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;
            string dll = parameters?.GetValue(ServiceDllValue, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;
            object start = service.GetValue(StartValue, string.Empty) ?? string.Empty;
            yield return new PersistenceItem(PersistenceKind.Service, name, string.Create(CultureInfo.InvariantCulture,
                $"type={type}; start={start}; image={image}{(dll.Length == 0 ? string.Empty : "; dll=" + dll)}"));
        }
    }

    /// <summary>The scheduled tasks, keyed by task path.</summary>
    internal static List<PersistenceItem> Tasks()
    {
        List<PersistenceItem> items = [];
        using var service = new TaskService();
        AddTasks(service.RootFolder, items);
        return items;
    }

    private static void AddTasks(TaskFolder folder, List<PersistenceItem> items)
    {
        try
        {
            foreach (Microsoft.Win32.TaskScheduler.Task task in folder.Tasks)
            {
                using (task)
                {
                    string actions = string.Join(" | ", task.Definition.Actions.Select(Describe));
                    items.Add(new PersistenceItem(PersistenceKind.ScheduledTask, task.Path,
                        string.Create(CultureInfo.InvariantCulture, $"enabled={task.Enabled}; {actions}")));
                }
            }

            foreach (TaskFolder child in folder.SubFolders)
            {
                using (child)
                {
                    AddTasks(child, items);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or COMException or FileNotFoundException)
        {
            // A folder this process may not read (or one deleted mid-walk) is left out, not fatal.
        }
    }

    private static string Describe(Microsoft.Win32.TaskScheduler.Action action) => action switch
    {
        ExecAction exec => string.Create(CultureInfo.InvariantCulture, $"{exec.Path} {exec.Arguments}").TrimEnd(),
        ComHandlerAction com => string.Create(CultureInfo.InvariantCulture, $"com:{com.ClassId:B}"),
        _ => action.ActionType.ToString(),
    };

    private static IEnumerable<PersistenceItem> RunValues(string? userSid)
    {
        IEnumerable<PersistenceItem> machine = new[] { WindowsStartupSource.RunPath, WindowsStartupSource.Run32Path, RunOncePath, RunOnce32Path }
            .SelectMany(path => RunItems(null, MachineLabel, path));
        return string.IsNullOrEmpty(userSid)
            ? machine
            : machine.Concat(new[] { WindowsStartupSource.RunPath, RunOncePath }.SelectMany(path => RunItems(userSid, UserLabel, path)));
    }

    private static IEnumerable<PersistenceItem> RunItems(string? userSid, string label, string path)
        => RegistryEnumeration.StringValues(userSid, path).Select(v =>
            new PersistenceItem(PersistenceKind.RunKey, string.Create(CultureInfo.InvariantCulture, $@"{label}\{path}\{v.Key}"), v.Value));

    private IEnumerable<PersistenceItem> StartupFiles(string? userSid)
        => _startup.List(userSid)
            .Where(e => e.Location is StartupLocation.MachineStartupFolder or StartupLocation.UserStartupFolder)
            .Select(e => new PersistenceItem(PersistenceKind.StartupFolder, e.Command, FileDetail(e.Command)));

    private static string FileDetail(string path)
    {
        var file = new FileInfo(path);
        return file.Exists
            ? string.Create(CultureInfo.InvariantCulture, $"{file.Length} bytes, modified {file.LastWriteTimeUtc:u}")
            : "missing";
    }
}
