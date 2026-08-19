using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Windows.Interop;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Writes and deletes machine-scope policy registry values through the Local
/// Group Policy object so they are recorded in the Local GPO's
/// <c>Registry.pol</c> and survive <c>gpupdate</c>. The COM object requires an
/// STA thread, so every operation runs on a short-lived STA worker.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class LocalGroupPolicy
{
    // GPClass coclass CLSID.
    private static readonly Guid GpClass = new("EA502722-A23D-11d1-A7D3-0000F87571E3");

    // The built-in registry client-side extension GUID (its CSE processes Registry.pol on gpupdate).
    private static readonly Guid RegistryExtension = new("35378EAC-683F-11D2-A89A-00C04FBBCFA2");

    // This product's own snap-in identifier recorded alongside the change.
    private static readonly Guid SnapinGuid = new("0EA5D050-9A2B-4F3C-8D1E-6B7C8A9D0E1F");

    private const uint GpoOpenLoadRegistry = 0x00000001;
    private const uint GpoSectionMachine = 2;

    /// <summary>Writes one machine-policy value into the Local GPO and saves it.</summary>
    /// <remarks>Retried on a brief <c>Registry.pol</c> sharing violation (see <see cref="LocalGroupPolicyRetry"/>).</remarks>
    public static void Write(string path, string name, object value, RegistryValueKind kind)
        => LocalGroupPolicyRetry.Run(
            () => RunSta(() =>
            {
                IGroupPolicyObject gpo = Open();
                using RegistryKey root = MachineRoot(gpo);
                using RegistryKey key = root.CreateSubKey(path, writable: true);
                key.SetValue(name, value, kind);
                Save(gpo);
            }),
            Thread.Sleep);

    /// <summary>Deletes one machine-policy value from the Local GPO and saves it.</summary>
    /// <remarks>Retried on a brief <c>Registry.pol</c> sharing violation (see <see cref="LocalGroupPolicyRetry"/>).</remarks>
    public static void Delete(string path, string name)
        => LocalGroupPolicyRetry.Run(
            () => RunSta(() =>
            {
                IGroupPolicyObject gpo = Open();
                using RegistryKey root = MachineRoot(gpo);
                using RegistryKey? key = root.OpenSubKey(path, writable: true);
                key?.DeleteValue(name, throwOnMissingValue: false);
                Save(gpo);
            }),
            Thread.Sleep);

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:Recognized reflection pattern",
        Justification = "Activates a fixed in-box COM server (GPClass) by CLSID; the type is an OS COM class, not managed " +
            "code the trimmer can remove, so there is nothing to preserve.")]
    private static IGroupPolicyObject Open()
    {
        Type type = Type.GetTypeFromCLSID(GpClass)
            ?? throw new InvalidOperationException("The Group Policy COM class is unavailable (Home edition or disabled GP engine).");
        var gpo = (IGroupPolicyObject)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not create the Group Policy object."));
        gpo.OpenLocalMachineGPO(GpoOpenLoadRegistry);
        return gpo;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "RegistryKey.FromHandle takes ownership of the SafeRegistryHandle; disposing the returned key (the " +
            "caller's using statement) disposes the handle.")]
    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "Ownership of the SafeRegistryHandle transfers to the returned RegistryKey, which the caller disposes.")]
    private static RegistryKey MachineRoot(IGroupPolicyObject gpo)
    {
        nint handle = gpo.GetRegistryKey(GpoSectionMachine);
        if (handle == nint.Zero)
        {
            throw new InvalidOperationException("The Local GPO did not return a machine registry key.");
        }

        var safeHandle = new Microsoft.Win32.SafeHandles.SafeRegistryHandle(handle, ownsHandle: true);
        return RegistryKey.FromHandle(safeHandle, RegistryView.Default);
    }

    private static void Save(IGroupPolicyObject gpo)
    {
        Guid extension = RegistryExtension;
        Guid snapin = SnapinGuid;
        gpo.Save(machine: true, add: true, in extension, in snapin);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The STA worker must marshal any failure (COMException, registry, security) back to the calling " +
            "thread as a single fault; the caller's transactional boundary decides what to do. Recorded in PLAN.md section 7.3.")]
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        worker.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Local Group Policy operation failed: {failure.Message}"), failure);
        }
    }
}
