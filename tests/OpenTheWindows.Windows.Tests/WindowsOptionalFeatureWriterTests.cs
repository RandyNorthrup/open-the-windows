using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The DISM verbs, timeouts and outcomes the optional-feature writer derives, driven
/// through a recording command runner (nothing is changed on the machine).
/// </summary>
public sealed class WindowsOptionalFeatureWriterTests
{
    private const int UnknownFeature = unchecked((int)0x800F080C);

    [Theory]
    [InlineData("WMIC~~~~", true, "/add-capability", "/capabilityname:WMIC~~~~", 60)]
    [InlineData("WMIC~~~~", false, "/remove-capability", "/capabilityname:WMIC~~~~", 15)]
    [InlineData("WindowsMediaPlayer", true, "/enable-feature", "/featurename:WindowsMediaPlayer", 15)]
    [InlineData("WindowsMediaPlayer", false, "/disable-feature", "/featurename:WindowsMediaPlayer", 15)]
    public void Each_change_runs_its_dism_verb_with_a_fitting_timeout(string name, bool enabled, string verb, string nameSwitch, int minutes)
    {
        var runner = new RecordingRunner(new CommandResult(0, string.Empty, string.Empty, TimedOut: false));

        new WindowsOptionalFeatureWriter(runner).SetEnabled(name, enabled);

        Assert.Equal("dism.exe", runner.Executable);
        Assert.Equal(["/online", verb, nameSwitch, "/norestart"], runner.Arguments);
        Assert.Equal(TimeSpan.FromMinutes(minutes), runner.Timeout);
    }

    [Theory]
    [InlineData(3010)]
    [InlineData(UnknownFeature)]
    public void A_reboot_required_exit_or_an_already_absent_feature_is_success_for_a_disable(int exitCode)
    {
        var writer = new WindowsOptionalFeatureWriter(new RecordingRunner(new CommandResult(exitCode, string.Empty, string.Empty, TimedOut: false)));

        Exception? failure = Record.Exception(() => writer.SetEnabled("WindowsMediaPlayer", enabled: false));

        Assert.Null(failure);
    }

    [Fact]
    public void Enabling_a_feature_windows_does_not_have_fails()
    {
        var writer = new WindowsOptionalFeatureWriter(new RecordingRunner(new CommandResult(UnknownFeature, string.Empty, "unknown", TimedOut: false)));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => writer.SetEnabled("WindowsMediaPlayer", enabled: true));

        Assert.Contains("/enable-feature", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_stopped_for_time_says_so()
    {
        var writer = new WindowsOptionalFeatureWriter(new RecordingRunner(new CommandResult(-1, string.Empty, string.Empty, TimedOut: true)));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => writer.SetEnabled("WMIC~~~~", enabled: true));

        Assert.Contains("did not finish in time", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_runner_or_name_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsOptionalFeatureWriter(null!));
        Assert.ThrowsAny<ArgumentException>(() => new WindowsOptionalFeatureWriter(new RecordingRunner(new CommandResult(0, string.Empty, string.Empty, TimedOut: false))).SetEnabled(" ", enabled: true));
    }

    private sealed class RecordingRunner(CommandResult result) : ICommandRunner
    {
        public string? Executable { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public TimeSpan Timeout { get; private set; }

        public CommandResult Run(string executable, IReadOnlyList<string> args, TimeSpan timeout)
        {
            Executable = executable;
            Arguments = args;
            Timeout = timeout;
            return result;
        }
    }
}
