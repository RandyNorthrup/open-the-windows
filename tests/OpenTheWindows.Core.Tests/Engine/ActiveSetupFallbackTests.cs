using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Tests.Engine;

/// <summary>
/// Tests the pure Active Setup component builder: the deterministic key name, the
/// quoted <c>remediate --user-scope</c> stub command, and the id validation that
/// stops an untrusted profile id from injecting characters into the registry path.
/// </summary>
public sealed class ActiveSetupFallbackTests
{
    private const string Exe = @"C:\Program Files\Open The Windows\otw.exe";

    [Fact]
    public void For_builds_the_braced_component_key()
    {
        ActiveSetupComponent component = ActiveSetupFallback.For("enterprise-workstation", "OTW Enterprise", Exe);

        Assert.Equal("{OTW-enterprise-workstation}", component.ComponentKey);
        Assert.StartsWith(ActiveSetupFallback.ComponentKeyPrefix, component.ComponentKey, StringComparison.Ordinal);
    }

    [Fact]
    public void For_quotes_the_executable_and_passes_user_scope_and_profile()
    {
        ActiveSetupComponent component = ActiveSetupFallback.For("home", "OTW Home", Exe);

        Assert.Equal(
            "\"C:\\Program Files\\Open The Windows\\otw.exe\" remediate --user-scope --profile home",
            component.StubPath);
    }

    [Fact]
    public void For_keeps_the_display_name()
    {
        ActiveSetupComponent component = ActiveSetupFallback.For("home", "Open the Windows - Home", Exe);

        Assert.Equal("Open the Windows - Home", component.DisplayName);
    }

    [Theory]
    [InlineData("Enterprise")]   // upper-case
    [InlineData("a b")]          // space
    [InlineData(@"..\evil")]     // path traversal
    [InlineData("x}y")]          // brace injection
    [InlineData("q\\w")]         // backslash
    [InlineData("a/b")]          // forward slash
    public void For_rejects_a_non_kebab_id(string id)
        => Assert.Throws<ArgumentException>(() => ActiveSetupFallback.For(id, "name", Exe));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void For_rejects_a_blank_id(string id)
        => Assert.Throws<ArgumentException>(() => ActiveSetupFallback.For(id, "name", Exe));
}
