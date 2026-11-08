namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// An installer's own temporary files queued for deletion at the next restart do not
/// block an apply; a file waiting to be replaced, or a deletion elsewhere, does.
/// </summary>
public sealed class PendingFileRenamesTests
{
    [Fact]
    public void An_empty_queue_needs_no_restart()
        => Assert.False(PendingFileRenames.RequireReboot([]));

    [Fact]
    public void Temporary_files_queued_for_deletion_need_no_restart()
    {
        string[] queue =
        [
            @"*1\??\C:\Users\technician\AppData\Local\Temp\~nsu1.tmp", string.Empty,
            @"\??\C:\Users\technician\AppData\Local\Temp\~nsu1.tmp\Un.exe", string.Empty,
            @"\??\C:\WINDOWS\TEMP\setup.log", string.Empty,
            @"\??\C:\Windows\SystemTemp\x.dat", string.Empty,
            string.Empty, string.Empty,
        ];

        Assert.False(PendingFileRenames.RequireReboot(queue));
    }

    [Theory]
    [InlineData(@"\??\C:\Windows\System32\drivers\old.sys", "")]
    [InlineData(@"\??\C:\Users\technician\AppData\Local\Temp\new.dll", @"!\??\C:\Program Files\App\in-use.dll")]
    [InlineData(@"\??\C:\Users\technician\AppData\Local\Temp\..\..\Roaming\App\settings.dat", "")]
    public void A_replacement_or_a_deletion_outside_temp_needs_a_restart(string source, string destination)
        => Assert.True(PendingFileRenames.RequireReboot([@"\??\C:\Windows\Temp\a.tmp", string.Empty, source, destination]));

    [Fact]
    public void A_trailing_source_without_its_destination_still_counts()
        => Assert.True(PendingFileRenames.RequireReboot([@"\??\C:\Program Files\App\old.exe"]));

    [Fact]
    public void A_missing_queue_is_refused()
        => Assert.Throws<ArgumentNullException>(() => PendingFileRenames.RequireReboot(null!));
}
