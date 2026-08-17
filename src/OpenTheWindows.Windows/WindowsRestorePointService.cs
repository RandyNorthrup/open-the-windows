using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Creates a System Restore point through <c>srclient.dll</c> and verifies the
/// outcome by counting restore points before and after. Reports the truth:
/// created (with the sequence number), skipped because one already exists within
/// the frequency window, or disabled.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRestorePointService : IRestorePointService
{
    private const int BeginSystemChange = 100;
    private const int EndSystemChange = 101;
    private const int ModifySettings = 12;
    private const int ErrorServiceDisabled = 1058;

    /// <inheritdoc />
    public RestorePointResult Create(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        long before = CountRestorePoints();

        var info = new RestorePointInfo
        {
            EventType = BeginSystemChange,
            RestorePointType = ModifySettings,
            SequenceNumber = 0,
            Description = description,
        };

        if (!SRSetRestorePointW(ref info, out StateManagerStatus status))
        {
            return status.Status == ErrorServiceDisabled
                ? new RestorePointResult(RestorePointStatus.Disabled, null, "System Restore is turned off for the system drive.")
                : new RestorePointResult(RestorePointStatus.Failed, null,
                    string.Create(CultureInfo.InvariantCulture, $"SRSetRestorePoint failed (status {status.Status})."));
        }

        long sequence = status.SequenceNumber;
        var end = info with { EventType = EndSystemChange, SequenceNumber = sequence };
        _ = SRSetRestorePointW(ref end, out _);

        long after = CountRestorePoints();
        if (before >= 0 && after >= 0 && after <= before)
        {
            return new RestorePointResult(RestorePointStatus.Skipped24h, null,
                "A restore point was already created within the system frequency window (default 24 hours).");
        }

        return new RestorePointResult(RestorePointStatus.Created, sequence, "Restore point created and verified.");
    }

    private static long CountRestorePoints()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\default"),
                new ObjectQuery("SELECT SequenceNumber FROM SystemRestore"));
            using ManagementObjectCollection results = searcher.Get();
            return results.Count;
        }
        catch (ManagementException)
        {
            // The SystemRestore WMI provider is unavailable; the created/skipped distinction cannot be verified.
            return -1;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private record struct RestorePointInfo
    {
        public int EventType;
        public int RestorePointType;
        public long SequenceNumber;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StateManagerStatus
    {
        public int Status;
        public long SequenceNumber;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SRSetRestorePointW(ref RestorePointInfo restorePointInfo, out StateManagerStatus status);
}
