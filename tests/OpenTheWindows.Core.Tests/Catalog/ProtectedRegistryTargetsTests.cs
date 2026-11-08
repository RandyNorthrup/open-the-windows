using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

/// <summary>
/// The code-level registry backstop: code-execution and persistence locations are
/// refused in both directions (including deletes), protected services' start
/// configuration is refused, and weakening values are refused on apply but not on
/// restore.
/// </summary>
public sealed class ProtectedRegistryTargetsTests
{
    [Theory]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\sethc.exe", "Debugger")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Updater")]
    [InlineData(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", "x")]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit")]
    [InlineData(@"SYSTEM\CurrentControlSet\Control\Lsa", "Security Packages")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\Fax", "ImagePath")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\Fax\Parameters", "ServiceDll")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\wuauserv", "Start")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\Fax\Security", "Security")]
    [InlineData(@"SOFTWARE\Microsoft\Windows Defender\Exclusions\Paths", @"C:\x")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "DataBasePath")]
    public void Code_execution_and_protected_locations_are_refused_in_every_direction(string path, string name)
    {
        Assert.NotNull(ProtectedRegistryTargets.Refusal(path, name, Text("x"), restoring: false));
        Assert.NotNull(ProtectedRegistryTargets.Refusal(path, name, Text("x"), restoring: true));
        Assert.NotNull(ProtectedRegistryTargets.Refusal(path, name, value: null, restoring: true));
    }

    [Theory]
    [InlineData(@"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\x.exe")]
    [InlineData(@"\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\x.exe\")]
    [InlineData(@"SOFTWARE/Microsoft/Windows NT/CurrentVersion/Image File Execution Options/x.exe")]
    public void A_rule_cannot_be_dodged_by_spelling_the_key_another_way(string path)
        => Assert.NotNull(ProtectedRegistryTargets.Refusal(path, "Debugger", Text("x"), restoring: false));

    [Fact]
    public void A_numbered_control_set_is_treated_as_the_current_one()
        => Assert.NotNull(ProtectedRegistryTargets.Refusal(@"SYSTEM\ControlSet001\Services\WinDefend", "Start", Number(4), restoring: false));

    [Theory]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\MessagingService", "Start")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters", "EnableMDNS")]
    [InlineData(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL")]
    [InlineData(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry")]
    public void Ordinary_settings_are_permitted(string path, string name)
        => Assert.Null(ProtectedRegistryTargets.Refusal(path, name, Number(0), restoring: false));

    [Fact]
    public void A_com_server_path_may_be_emptied_but_never_pointed_at_a_binary()
    {
        const string Server = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
        Assert.Null(ProtectedRegistryTargets.Refusal(Server, string.Empty, Text(string.Empty), restoring: false));
        Assert.Null(ProtectedRegistryTargets.Refusal(Server, string.Empty, value: null, restoring: true));
        Assert.NotNull(ProtectedRegistryTargets.Refusal(Server, string.Empty, Text(@"C:\evil.dll"), restoring: false));
        Assert.NotNull(ProtectedRegistryTargets.Refusal(@"SOFTWARE\Classes\exefile\shell\open\command", string.Empty, Text("cmd"), restoring: false));
    }

    [Theory]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0)]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 0)]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "LocalAccountTokenFilterPolicy", 1)]
    [InlineData(@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1)]
    [InlineData(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1)]
    [InlineData(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\PublicProfile", "EnableFirewall", 0)]
    [InlineData(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0)]
    [InlineData(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DisableWindowsUpdateAccess", 1)]
    public void A_weakening_value_is_refused_on_apply_but_not_when_restoring(string path, string name, int value)
    {
        Assert.NotNull(ProtectedRegistryTargets.Refusal(path, name, Number(value), restoring: false));
        Assert.Null(ProtectedRegistryTargets.Refusal(path, name, Number(value), restoring: true));
    }

    [Fact]
    public void The_secure_direction_of_a_weakening_rule_is_permitted()
    {
        Assert.Null(ProtectedRegistryTargets.Refusal(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", Number(1), restoring: false));
        Assert.Null(ProtectedRegistryTargets.Refusal(@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", Number(0), restoring: false));
        Assert.NotNull(ProtectedRegistryTargets.Refusal(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", JsonSerializer.SerializeToElement("Off"), restoring: false));
    }
}
