using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Windows.Readers;

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
}
