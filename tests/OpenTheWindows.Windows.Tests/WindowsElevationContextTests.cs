using System.Security.Principal;

namespace OpenTheWindows.Windows.Tests;

public sealed class WindowsElevationContextTests
{
    [Fact]
    public void Reports_the_process_user_and_a_consistent_elevation_flag()
    {
        var sut = new WindowsElevationContext();

        using var identity = WindowsIdentity.GetCurrent();
        bool expectedElevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);

        Assert.Equal(identity.Name, sut.ProcessUserName);
        Assert.Equal(expectedElevated, sut.IsElevated);
        Assert.Contains('\\', sut.ProcessUserName);
    }
}
