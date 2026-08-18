using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class UsersCommandTests
{
    [Fact]
    public void Lists_each_user_profile_with_its_loaded_state()
    {
        var (root, stdout, stderr) = TestCli.UsersHost(new FakeUserHiveEnumerator(
            new UserProfile("S-1-5-21-1-2-3-1001", @"C:\Users\alice", HiveLoaded: true),
            new UserProfile("S-1-5-21-1-2-3-1002", @"C:\Users\bob", HiveLoaded: false)));

        int code = CliTestHost.Invoke(root, stdout, stderr, "users");

        Assert.Equal(ExitCodes.Success, code);
        string output = stdout.ToString();
        Assert.Contains("S-1-5-21-1-2-3-1001", output, StringComparison.Ordinal);
        Assert.Contains(@"C:\Users\bob", output, StringComparison.Ordinal);
        Assert.Contains("not loaded", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_lists_the_profiles()
    {
        var (root, stdout, stderr) = TestCli.UsersHost(new FakeUserHiveEnumerator(
            new UserProfile("S-1-5-21-9-9-9-500", @"C:\Users\admin", HiveLoaded: true)));

        int code = CliTestHost.Invoke(root, stdout, stderr, "users", "--json");

        Assert.Equal(ExitCodes.Success, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        JsonElement first = document.RootElement[0];
        Assert.Equal("S-1-5-21-9-9-9-500", first.GetProperty("sid").GetString());
        Assert.True(first.GetProperty("hiveLoaded").GetBoolean());
    }

    [Fact]
    public void Reports_when_no_user_profiles_exist()
    {
        var (root, stdout, stderr) = TestCli.UsersHost(new FakeUserHiveEnumerator());

        int code = CliTestHost.Invoke(root, stdout, stderr, "users");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("No user profiles", stdout.ToString(), StringComparison.Ordinal);
    }
}
