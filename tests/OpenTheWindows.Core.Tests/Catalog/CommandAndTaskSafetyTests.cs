using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Tests.Catalog;

/// <summary>
/// Argument shapes for the allow-listed catalogue commands, the scheduled-task folders
/// that are never disabled, and the security-policy template values.
/// </summary>
public sealed class CommandAndTaskSafetyTests
{
    [Theory]
    [InlineData("powercfg.exe", "/setactive|381b4222-f694-41f0-9685-ff5bb260df2e")]
    [InlineData("powercfg.exe", "-duplicatescheme|e9a42b02-d5df-448d-aa00-03f14749eb61")]
    [InlineData("powercfg.exe", "-duplicatescheme|e9a42b02-d5df-448d-aa00-03f14749eb61|3ff9831b-6f80-4830-8178-736cd4229e7b")]
    [InlineData("powercfg.exe", "-delete|3ff9831b-6f80-4830-8178-736cd4229e7b")]
    [InlineData("powercfg.exe", "/hibernate|off")]
    [InlineData("powercfg.exe", "/hibernate|/type|reduced")]
    [InlineData("fsutil.exe", "behavior|set|disable8dot3|1")]
    [InlineData("auditpol.exe", "/set|/subcategory:Other Logon/Logoff Events|/success:enable|/failure:enable")]
    public void Permitted_command_shapes_pass(string executable, string arguments)
        => Assert.Null(CommandArgumentPolicy.Refusal(executable, arguments.Split('|')));

    [Theory]
    [InlineData("powercfg.exe", "/import|C:\\evil.pow")]
    [InlineData("powercfg.exe", "/setactive|SCHEME_MIN")]
    [InlineData("powercfg.exe", "-delete|e9a42b02-d5df-448d-aa00-03f14749eb61")]
    [InlineData("powercfg.exe", "/delete|381b4222-f694-41f0-9685-ff5bb260df2e")]
    [InlineData("powercfg.exe", "-duplicatescheme|e9a42b02-d5df-448d-aa00-03f14749eb61|381b4222-f694-41f0-9685-ff5bb260df2e")]
    [InlineData("powercfg.exe", "-duplicatescheme|e9a42b02-d5df-448d-aa00-03f14749eb61|mine")]
    [InlineData("powercfg.exe", "/setactive|381b4222-f694-41f0-9685-ff5bb260df2e|extra")]
    [InlineData("fsutil.exe", "file|setZeroData|C:\\x")]
    [InlineData("auditpol.exe", "/clear|/y")]
    [InlineData("auditpol.exe", "/set|/subcategory:Logon\"|/success:enable")]
    [InlineData("auditpol.exe", "/set|/subcategory:Logon|/remove")]
    [InlineData("netsh.exe", "add|helper|C:\\evil.dll")]
    [InlineData("cmd.exe", "/c|whoami")]
    public void Other_commands_and_shapes_are_refused(string executable, string arguments)
        => Assert.NotNull(CommandArgumentPolicy.Refusal(executable, arguments.Split('|')));

    [Theory]
    [InlineData(@"\Microsoft\Windows\WindowsUpdate\Scheduled Start")]
    [InlineData(@"\Microsoft\Windows\UpdateOrchestrator\Schedule Scan")]
    [InlineData(@"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan")]
    public void Servicing_and_security_tasks_are_never_disabled(string path)
    {
        Assert.NotNull(ProtectedScheduledTasks.Refusal(path, enabled: false));
        Assert.Null(ProtectedScheduledTasks.Refusal(path, enabled: true));
    }

    [Fact]
    public void Other_tasks_may_be_disabled()
        => Assert.Null(ProtectedScheduledTasks.Refusal(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator", enabled: false));

    [Theory]
    [InlineData("MinimumPasswordLength", "14", true)]
    [InlineData("LockoutBadCount", "-1", true)]
    [InlineData("MinimumPasswordLength", "14\r\n[Group Membership]", false)]
    [InlineData("Minimum Password", "14", false)]
    [InlineData("MinimumPasswordLength", "fourteen", false)]
    [InlineData("MinimumPasswordLength", "", false)]
    public void Security_policy_values_must_be_a_letters_only_name_and_an_integer(string setting, string value, bool safe)
        => Assert.Equal(safe, SecurityPolicyValues.Refusal(setting, value) is null);
}
