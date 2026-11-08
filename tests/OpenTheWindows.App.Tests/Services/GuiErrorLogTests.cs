using System.IO;
using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>The GUI error log appends a timestamped fault and never throws while doing so; the fault filter spares only fatal faults.</summary>
public sealed class GuiErrorLogTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Append_creates_the_folder_and_appends_each_fault()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"otw-gui-log-{Guid.NewGuid():N}");
        string path = Path.Combine(folder, "gui-errors.log");
        try
        {
            Assert.True(GuiErrorLog.Append(path, At, new InvalidOperationException("first fault")));
            Assert.True(GuiErrorLog.Append(path, At, new InvalidOperationException("second fault")));

            string text = File.ReadAllText(path);
            Assert.Contains("[2026-11-07 09:00:00Z]", text, StringComparison.Ordinal);
            Assert.Contains("first fault", text, StringComparison.Ordinal);
            Assert.Contains("second fault", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Append_reports_failure_instead_of_throwing_when_the_log_cannot_be_written()
    {
        // The log's "folder" is an existing file, so the folder cannot be created.
        string blocker = Path.GetTempFileName();
        try
        {
            Assert.False(GuiErrorLog.Append(Path.Combine(blocker, "gui-errors.log"), At, new InvalidOperationException("fault")));
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [Fact]
    public void Append_rejects_missing_arguments()
    {
        Assert.Throws<ArgumentException>(() => GuiErrorLog.Append(" ", At, new InvalidOperationException("fault")));
        Assert.Throws<ArgumentNullException>(() => GuiErrorLog.Append("x.log", At, null!));
    }

    [Fact]
    public void The_default_path_is_under_local_app_data()
    {
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GuiErrorLog.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("OpenTheWindows", "gui-errors.log"), GuiErrorLog.DefaultPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_fatal_faults_are_not_reportable()
    {
        Assert.True(UiFaults.IsReportable(new InvalidOperationException("x")));
        Assert.True(UiFaults.IsReportable(new UnauthorizedAccessException("x")));
        Assert.False(UiFaults.IsReportable(new InsufficientExecutionStackException()));
    }
}
