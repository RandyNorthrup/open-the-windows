using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Reads OS identity from <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion</c>.
/// The registry is preferred over <see cref="Environment.OSVersion"/> because it
/// carries the edition, marketing version and UBR, and is not subject to
/// application-manifest version lies.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsOperatingSystemInfo : IOperatingSystemInfo
{
    private const string CurrentVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    /// <summary>
    /// The registry still reports "Windows 10 ..." as ProductName on Windows 11
    /// (documented behaviour); the product name is corrected from the build number.
    /// </summary>
    private const string Windows10Prefix = "Windows 10";
    private const string Windows11Prefix = "Windows 11";

    /// <inheritdoc />
    public OperatingSystemFacts GetFacts()
    {
        using RegistryKey key = Registry.LocalMachine.OpenSubKey(CurrentVersionKeyPath, writable: false)
            ?? throw new InvalidOperationException(
                $@"Registry key HKLM\{CurrentVersionKeyPath} is missing; cannot determine the Windows version.");

        string productName = ReadString(key, "ProductName");
        string editionId = ReadString(key, "EditionID");
        string displayVersion = ReadString(key, "DisplayVersion");
        string installationType = ReadString(key, "InstallationType");
        int build = ReadInt32FromString(key, "CurrentBuildNumber");
        int revision = ReadInt32(key, "UBR");

        if (build >= OperatingSystemFacts.FirstWindows11Build
            && productName.StartsWith(Windows10Prefix, StringComparison.Ordinal))
        {
            productName = string.Concat(Windows11Prefix, productName.AsSpan(Windows10Prefix.Length));
        }

        return new OperatingSystemFacts(
            productName,
            editionId,
            displayVersion,
            build,
            revision,
            ArchitectureName(RuntimeInformation.OSArchitecture),
            installationType);
    }

    /// <summary>Stable lower-case names used in reports and catalogue <c>appliesTo</c> filters.</summary>
    private static string ArchitectureName(Architecture architecture)
        => architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => architecture.ToString(),
        };

    private static string ReadString(RegistryKey key, string valueName)
    {
        object? raw = key.GetValue(valueName);
        return raw is string s
            ? s
            : throw new InvalidOperationException(
                $@"Registry value HKLM\{CurrentVersionKeyPath}\{valueName} is missing or not a string.");
    }

    private static int ReadInt32(RegistryKey key, string valueName)
    {
        object? raw = key.GetValue(valueName);
        return raw is int i
            ? i
            : throw new InvalidOperationException(
                $@"Registry value HKLM\{CurrentVersionKeyPath}\{valueName} is missing or not a DWORD.");
    }

    private static int ReadInt32FromString(RegistryKey key, string valueName)
    {
        string text = ReadString(key, valueName);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new InvalidOperationException(
                $@"Registry value HKLM\{CurrentVersionKeyPath}\{valueName} ('{text}') is not a number.");
    }
}
