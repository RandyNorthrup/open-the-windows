using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real scheduled tasks through the Task Scheduler API (read-only, no
/// elevation needed). Uses a task registered on every Windows 11 installation.
/// </summary>
public sealed class WindowsScheduledTaskReaderTests
{
    // Built-in defragmentation maintenance task, present on every Windows 11 box.
    private const string DefragTaskPath = @"\Microsoft\Windows\Defrag\ScheduledDefrag";

    [Fact]
    public void Reads_a_built_in_task()
    {
        var reader = new WindowsScheduledTaskReader();

        ScheduledTaskSnapshot? snapshot = reader.Read(DefragTaskPath);

        Assert.NotNull(snapshot);
        Assert.Equal(DefragTaskPath, snapshot.Path);
        Assert.True(snapshot.Exists);
    }

    [Fact]
    public void Missing_task_is_null()
    {
        var reader = new WindowsScheduledTaskReader();

        ScheduledTaskSnapshot? snapshot = reader.Read(@"\OpenTheWindows\NoSuchTask");

        Assert.Null(snapshot);
    }
}
