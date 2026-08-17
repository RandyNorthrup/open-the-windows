using System.Security.Principal;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Resolves the interactive session user through the Terminal Services API
/// (read-only, no elevation needed). The test process runs unelevated as the
/// signed-in user, so the resolved SID must match the current identity.
/// </summary>
public sealed class WindowsInteractiveUserResolverTests
{
    [Fact]
    public void Resolves_the_signed_in_user()
    {
        var resolver = new WindowsInteractiveUserResolver();

        InteractiveUser? user = resolver.Resolve();

        Assert.NotNull(user);
        string expectedSid = WindowsIdentity.GetCurrent().User!.Value;
        Assert.Equal(expectedSid, user.Sid);
        Assert.False(string.IsNullOrEmpty(user.UserName));
        Assert.True(user.HiveLoaded);
    }
}
