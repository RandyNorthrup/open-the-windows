using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Parsing of <c>dism /English</c> feature and capability state, including the
/// pending states WMI does not report.
/// </summary>
public sealed class DismStateTests
{
    [Theory]
    [InlineData("Feature Name : SMB1Protocol\r\nState : Enabled\r\n", true)]
    [InlineData("State : Disable Pending", false)]
    [InlineData("State : Enable Pending", true)]
    [InlineData("State : Disabled with Payload Removed", false)]
    [InlineData("Capability Identity : WMIC~~~~\r\nState : Installed", true)]
    [InlineData("State : Not Present", false)]
    [InlineData("State : Uninstall Pending", false)]
    [InlineData("State : Staged", false)]
    public void A_state_line_maps_to_the_state_it_is_heading_to(string output, bool expected)
        => Assert.Equal(expected, DismState.Parse(output));

    [Theory]
    [InlineData("")]
    [InlineData("Error: 740\r\nElevated permissions are required")]
    [InlineData("State : Something New")]
    public void Output_without_a_known_state_is_unknown(string output)
        => Assert.Null(DismState.Parse(output));

    [Theory]
    [InlineData("WMIC~~~~", true)]
    [InlineData("Print.Fax.Scan~~~~0.0.1.0", true)]
    [InlineData("SMB1Protocol", false)]
    public void Capabilities_are_recognised_by_their_identity_marker(string name, bool capability)
        => Assert.Equal(capability, DismState.IsCapability(name));
}
