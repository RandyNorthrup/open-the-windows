using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Profiles;

public sealed class ProfilePolicyTests
{
    private static ProfilePolicy Read(RegistryValueSnapshot snapshot)
        => ProfilePolicy.Read(new FakeReaders { OnRegistry = (_, _, _, _) => snapshot });

    [Fact]
    public void Absent_value_is_not_required()
        => Assert.False(Read(new RegistryValueSnapshot(false, null, null)).RequireSignedProfiles);

    [Fact]
    public void Dword_one_requires_signed_profiles()
        => Assert.True(Read(new RegistryValueSnapshot(true, RegistryValueType.Dword, JsonSerializer.SerializeToElement(1))).RequireSignedProfiles);

    [Fact]
    public void Dword_zero_does_not_require_signed_profiles()
        => Assert.False(Read(new RegistryValueSnapshot(true, RegistryValueType.Dword, JsonSerializer.SerializeToElement(0))).RequireSignedProfiles);

    [Fact]
    public void Unrestricted_is_not_required()
        => Assert.False(ProfilePolicy.Unrestricted.RequireSignedProfiles);

    [Fact]
    public void Reads_the_machine_policy_value()
    {
        RegistryHive? hive = null;
        string? path = null;
        string? name = null;
        var readers = new FakeReaders
        {
            OnRegistry = (h, _, p, n) =>
            {
                (hive, path, name) = (h, p, n);
                return new RegistryValueSnapshot(false, null, null);
            },
        };

        ProfilePolicy.Read(readers);

        Assert.Equal(RegistryHive.LocalMachine, hive);
        Assert.Equal(ProfilePolicy.PolicyKeyPath, path);
        Assert.Equal(ProfilePolicy.RequireSignedProfilesValueName, name);
    }
}
