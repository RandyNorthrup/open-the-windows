using System.Security.Principal;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Windows.Readers;
using Win32Key = Microsoft.Win32.RegistryKey;
using Win32Registry = Microsoft.Win32.Registry;
using Win32ValueKind = Microsoft.Win32.RegistryValueKind;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads the real registry (read-only, no elevation needed). Runs on any
/// supported Windows machine; assertions are invariant across builds.
/// </summary>
public sealed class WindowsRegistryReaderTests
{
    private const string CurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    [Fact]
    public void Reads_a_known_string_value()
    {
        var reader = new WindowsRegistryReader();

        RegistryValueSnapshot snapshot = reader.Read(RegistryHive.LocalMachine, null, CurrentVersion, "CurrentBuild");

        Assert.True(snapshot.Exists);
        Assert.Equal(RegistryValueType.Sz, snapshot.Type);
        Assert.NotNull(snapshot.Data);
        Assert.Equal(JsonValueKind.String, snapshot.Data.Value.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Data.Value.GetString()));
    }

    [Fact]
    public void Reads_a_known_dword_value_as_a_number()
    {
        var reader = new WindowsRegistryReader();

        RegistryValueSnapshot snapshot = reader.Read(RegistryHive.LocalMachine, null, CurrentVersion, "CurrentMajorVersionNumber");

        Assert.True(snapshot.Exists);
        Assert.Equal(RegistryValueType.Dword, snapshot.Type);
        Assert.NotNull(snapshot.Data);
        Assert.Equal(JsonValueKind.Number, snapshot.Data.Value.ValueKind);
        Assert.True(snapshot.Data.Value.GetUInt64() >= 10);
    }

    [Fact]
    public void Missing_value_is_reported_as_absent()
    {
        var reader = new WindowsRegistryReader();

        RegistryValueSnapshot snapshot = reader.Read(RegistryHive.LocalMachine, null, CurrentVersion, "OtwNoSuchValue");

        Assert.False(snapshot.Exists);
        Assert.Null(snapshot.Type);
        Assert.Null(snapshot.Data);
    }

    [Fact]
    public void Missing_key_is_reported_as_absent()
    {
        var reader = new WindowsRegistryReader();

        RegistryValueSnapshot snapshot = reader.Read(RegistryHive.LocalMachine, null, @"SOFTWARE\OpenTheWindows\NoSuchKey", "Any");

        Assert.False(snapshot.Exists);
    }

    [Fact]
    public void User_hive_without_a_sid_is_absent()
    {
        var reader = new WindowsRegistryReader();

        RegistryValueSnapshot snapshot = reader.Read(RegistryHive.User, null, @"Software\Microsoft", "Any");

        Assert.False(snapshot.Exists);
    }

    [Fact]
    public void Reads_every_value_type_from_the_user_hive()
    {
        // Tests may write to the current user's own hive (HKU\<own sid>); the
        // reader opens it via Registry.Users, exercising the user-hive path and
        // every value-type branch.
        string sid = WindowsIdentity.GetCurrent().User!.Value;
        const string SubPath = @"Software\OpenTheWindows\ReaderTest";
        using (Win32Key key = Win32Registry.CurrentUser.CreateSubKey(SubPath))
        {
            key.SetValue("dword", 7, Win32ValueKind.DWord);
            key.SetValue("qword", 8L, Win32ValueKind.QWord);
            key.SetValue("sz", "hi", Win32ValueKind.String);
            key.SetValue("expand", "%SystemRoot%", Win32ValueKind.ExpandString);
            key.SetValue("multi", new[] { "a", "b" }, Win32ValueKind.MultiString);
            key.SetValue("bin", new byte[] { 1, 2, 3 }, Win32ValueKind.Binary);
        }

        try
        {
            var reader = new WindowsRegistryReader();
            Assert.Equal(RegistryValueType.Dword, reader.Read(RegistryHive.User, sid, SubPath, "dword").Type);
            Assert.Equal(RegistryValueType.Qword, reader.Read(RegistryHive.User, sid, SubPath, "qword").Type);
            Assert.Equal(RegistryValueType.Sz, reader.Read(RegistryHive.User, sid, SubPath, "sz").Type);

            RegistryValueSnapshot expand = reader.Read(RegistryHive.User, sid, SubPath, "expand");
            Assert.Equal(RegistryValueType.ExpandSz, expand.Type);
            // Not expanded (DoNotExpandEnvironmentNames).
            Assert.Equal("%SystemRoot%", expand.Data!.Value.GetString());

            RegistryValueSnapshot multi = reader.Read(RegistryHive.User, sid, SubPath, "multi");
            Assert.Equal(RegistryValueType.MultiSz, multi.Type);
            Assert.Equal(JsonValueKind.Array, multi.Data!.Value.ValueKind);

            RegistryValueSnapshot binary = reader.Read(RegistryHive.User, sid, SubPath, "bin");
            Assert.Equal(RegistryValueType.Binary, binary.Type);
            Assert.Equal(JsonValueKind.String, binary.Data!.Value.ValueKind);
        }
        finally
        {
            Win32Registry.CurrentUser.DeleteSubKeyTree(SubPath, throwOnMissingSubKey: false);
        }
    }
}
