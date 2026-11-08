using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Tests.Catalog;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Engine;

/// <summary>
/// Catalogue commands are reverted to the state captured before they ran, not to a fixed
/// "off": hibernation, the 8.3 setting, the active power scheme and each audit subcategory.
/// When the state cannot be read, the declared revert arguments still run.
/// </summary>
public sealed class CommandStatesTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static readonly ApplyOptions Options = new(false, false, false, true, true, "test", false);

    private static TweakDefinition Entry(CommandAction action) => CatalogTestData.ValidEntry("performance.test.command") with { Actions = [action] };

    private static void SetDword(FakeMachine machine, string path, string name, int value)
        => machine.SetRegistry(RegistryHive.LocalMachine, null, path, name, RegistryValueType.Dword, JsonSerializer.SerializeToElement(value));

    // Applies the command, reverts the run, and returns the argument lists the revert ran.
    private static List<string> ApplyThenRevert(FakeMachine machine, CommandAction action)
    {
        ApplyHarness h = FakeApplyEngine.Create(machine);
        ApplyResult applied = h.Engine.Apply([Entry(action)], Options);
        Assert.Equal(RunState.Completed, applied.State);
        int before = machine.Commands.Count;

        RevertResult reverted = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Completed, reverted.State);
        return [.. machine.Commands.Skip(before)
            .Where(c => !c.Arguments.Contains("/r", StringComparer.Ordinal) && !c.Arguments.Contains("/getactivescheme", StringComparer.Ordinal))
            .Select(c => string.Join(' ', c.Arguments))];
    }

    [Fact]
    public void Hibernation_that_was_off_is_turned_off_again_not_on()
    {
        var machine = new FakeMachine();
        SetDword(machine, CommandStates.PowerPath, CommandStates.HibernateEnabledValue, 0);

        List<string> revert = ApplyThenRevert(machine, new CommandAction("powercfg.exe", ["/hibernate", "/type", "reduced"], ["/hibernate", "/type", "full"]));

        Assert.Equal(["/hibernate off"], revert);
    }

    [Theory]
    [InlineData(1, "/hibernate /type reduced")]
    [InlineData(2, "/hibernate /type full")]
    public void Hibernation_that_was_on_comes_back_with_its_file_type(int fileType, string typeCommand)
    {
        var machine = new FakeMachine();
        SetDword(machine, CommandStates.PowerPath, CommandStates.HibernateEnabledValue, 1);
        SetDword(machine, CommandStates.PowerPath, CommandStates.HiberFileTypeValue, fileType);

        List<string> revert = ApplyThenRevert(machine, new CommandAction("powercfg.exe", ["/hibernate", "off"], ["/hibernate", "on"]));

        Assert.Equal(["/hibernate on", typeCommand], revert);
    }

    [Fact]
    public void The_8dot3_setting_returns_to_its_prior_value()
    {
        var machine = new FakeMachine();
        SetDword(machine, CommandStates.FileSystemPath, CommandStates.Disable8Dot3Value, 2);

        List<string> revert = ApplyThenRevert(machine, new CommandAction("fsutil.exe", ["behavior", "set", "disable8dot3", "1"], ["behavior", "set", "disable8dot3", "0"]));

        Assert.Equal(["behavior set disable8dot3 2"], revert);
    }

    [Fact]
    public void The_prior_power_scheme_is_made_active_again()
    {
        var machine = new FakeMachine
        {
            CommandOutput = (_, args) => args.Contains("/getactivescheme", StringComparer.Ordinal) ? $"Power Scheme GUID: {Balanced}  (Balanced)" : string.Empty,
        };

        List<string> revert = ApplyThenRevert(machine, new CommandAction("powercfg.exe", ["/setactive", HighPerformance], ["/setactive", HighPerformance]));

        Assert.Equal([$"/setactive {Balanced}"], revert);
    }

    [Theory]
    [InlineData("Success and Failure", "/set /subcategory:Logon /success:enable /failure:enable")]
    [InlineData("Success", "/set /subcategory:Logon /success:enable /failure:disable")]
    [InlineData("Failure", "/set /subcategory:Logon /success:disable /failure:enable")]
    [InlineData("No Auditing", "/set /subcategory:Logon /success:disable /failure:disable")]
    public void An_audit_subcategory_returns_to_what_windows_audited_before(string inclusion, string expected)
    {
        var machine = new FakeMachine
        {
            CommandOutput = (_, args) => args.Contains("/r", StringComparer.Ordinal)
                ? $"Machine Name,Policy Target,Subcategory,Subcategory GUID,Inclusion Setting,Exclusion Setting\r\nPC,System,Logon,{{0CCE9215-69AE-11D9-BED3-505054503030}},{inclusion},\r\n"
                : string.Empty,
        };

        List<string> revert = ApplyThenRevert(machine, new CommandAction("auditpol.exe", ["/set", "/subcategory:Logon", "/success:enable", "/failure:enable"], ["/set", "/subcategory:Logon", "/success:disable", "/failure:disable"]));

        Assert.Equal([expected], revert);
    }

    [Fact]
    public void Without_a_readable_state_the_declared_revert_arguments_run()
    {
        var machine = new FakeMachine();

        List<string> revert = ApplyThenRevert(machine, new CommandAction("auditpol.exe", ["/set", "/subcategory:Logon", "/success:enable"], ["/set", "/subcategory:Logon", "/success:disable"]));

        Assert.Equal(["/set /subcategory:Logon /success:disable"], revert);
    }

    [Fact]
    public void Unknown_or_malformed_states_restore_nothing_typed()
    {
        var setActive = new CommandAction("powercfg.exe", ["/setactive", HighPerformance], ["/setactive", Balanced]);
        var hibernate = new CommandAction("powercfg.exe", ["/hibernate", "off"], ["/hibernate", "on"]);
        var shortNames = new CommandAction("fsutil.exe", ["behavior", "set", "disable8dot3", "1"], ["behavior", "set", "disable8dot3", "0"]);
        var audit = new CommandAction("auditpol.exe", ["/set", "/subcategory:Logon", "/success:enable"], ["/set", "/subcategory:Logon", "/success:disable"]);
        var duplicate = new CommandAction("powercfg.exe", ["/duplicatescheme", HighPerformance], ["/delete", HighPerformance]);

        Assert.Null(CommandStates.RestoreCommands(setActive, "scheme:not-a-guid"));
        Assert.Null(CommandStates.RestoreCommands(hibernate, "hibernate:sideways"));
        Assert.Null(CommandStates.RestoreCommands(shortNames, "8dot3:9"));
        Assert.Null(CommandStates.RestoreCommands(audit, "audit:7"));
        Assert.Null(CommandStates.RestoreCommands(audit, "scheme:" + Balanced));
        Assert.Null(CommandStates.RestoreCommands(duplicate, "scheme:" + Balanced));
        Assert.Null(CommandStates.Capture(duplicate, new FakeMachine(), new FakeMachine()));
        Assert.Null(CommandStates.Capture(new CommandAction("cmd.exe", ["/c", "x"], []), new FakeMachine(), new FakeMachine()));
    }

    [Fact]
    public void A_failing_or_unrecognised_read_captures_nothing()
    {
        var failing = new FakeMachine { CommandExitCode = 1, CommandOutput = (_, _) => $"Power Scheme GUID: {Balanced}" };
        var foreign = new FakeMachine { CommandOutput = (_, _) => "Computername,Richtlinienziel,Unterkategorie,GUID,Einschlusseinstellung,\r\nPC,System,Anmelden,{x},Erfolg und Fehler,\r\n" };
        var setActive = new CommandAction("powercfg.exe", ["/setactive", HighPerformance], ["/setactive", Balanced]);
        var audit = new CommandAction("auditpol.exe", ["/set", "/subcategory:Logon", "/success:enable"], ["/set", "/subcategory:Logon", "/success:disable"]);

        Assert.Null(CommandStates.Capture(setActive, failing, failing));
        Assert.Null(CommandStates.Capture(audit, foreign, foreign));
        Assert.Throws<ArgumentNullException>(() => CommandStates.Capture(null!, failing, failing));
        Assert.Throws<ArgumentNullException>(() => CommandStates.RestoreCommands(audit, null!));
    }

    [Fact]
    public void A_captured_state_is_journaled_with_the_action()
    {
        var machine = new FakeMachine();
        SetDword(machine, CommandStates.PowerPath, CommandStates.HibernateEnabledValue, 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult applied = h.Engine.Apply([Entry(new CommandAction("powercfg.exe", ["/hibernate", "off"], ["/hibernate", "on"]))], Options);

        JournalActionState? prior = h.Journal.Load(applied.RunId).Entries[0].Actions[0].Prior;
        Assert.Equal("hibernate:off", prior?.CommandState);
    }
}
