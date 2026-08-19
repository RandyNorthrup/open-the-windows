using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The Updates page: status load, pause presets, extended-pause warning, resume and the managed read-only mode.</summary>
public sealed class UpdatesViewModelTests
{
    private readonly FakeUpdateCoordinator _coordinator = new();
    private readonly FakeDialogService _dialog = new();

    private UpdatesViewModel Build() => new(_coordinator, _dialog);

    private static UpdateStatus Status(bool paused = false, bool warning = false, DateTimeOffset? until = null) => new("24H2", "Professional", EndOfServicingEditionFamily.Consumer, new DateOnly(2026, 10, 13), 56, warning, paused, until, null, null, false);

    [Fact]
    public void Is_the_updates_page()
    {
        UpdatesViewModel page = Build();

        Assert.Equal(PageKeys.Updates, page.Key);
        Assert.False(string.IsNullOrEmpty(page.Title));
    }

    [Fact]
    public void Refresh_loads_the_status()
    {
        _coordinator.StatusToReturn = Status();
        UpdatesViewModel page = Build();

        page.RefreshCommand.Execute(null);

        Assert.Contains("24H2", page.CurrentVersion, StringComparison.Ordinal);
        Assert.Contains("Not paused", page.PauseState, StringComparison.Ordinal);
        Assert.True(page.CanControl);
    }

    [Fact]
    public void The_status_is_not_read_until_the_user_refreshes()
    {
        _coordinator.StatusToReturn = Status();
        UpdatesViewModel page = Build();

        // No activation hook and nothing read on construction: the machine is
        // probed only when the user asks for it.
        Assert.Equal(0, _coordinator.StatusCount);
        Assert.DoesNotContain("24H2", page.CurrentVersion, StringComparison.Ordinal);
    }

    [Fact]
    public void A_paused_status_enables_resume()
    {
        _coordinator.StatusToReturn = Status(paused: true, until: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        UpdatesViewModel page = Build();

        page.RefreshCommand.Execute(null);

        Assert.True(page.IsPaused);
        Assert.True(page.CanResume);
        Assert.True(page.ResumeCommand.CanExecute(null));
    }

    [Fact]
    public void Managed_updates_are_read_only()
    {
        _coordinator.StatusToReturn = Status(paused: true, until: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        _coordinator.ManagedToReturn = true;
        UpdatesViewModel page = Build();

        page.RefreshCommand.Execute(null);

        Assert.True(page.IsManaged);
        Assert.False(page.CanControl);
        Assert.False(page.PauseCommand.CanExecute(7));
        Assert.False(page.ResumeCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_preset_pauses_for_that_many_days()
    {
        UpdatesViewModel page = Build();
        page.RefreshCommand.Execute(null);

        await page.PauseCommand.ExecuteAsync(14);

        Assert.Equal(1, _coordinator.PauseCount);
        Assert.Equal(14, _coordinator.LastPauseDays);
        Assert.False(_coordinator.LastPauseExtended);
    }

    [Fact]
    public async Task An_extended_pause_warns_and_pauses_when_confirmed()
    {
        _dialog.ConfirmResult = true;
        UpdatesViewModel page = Build();
        page.RefreshCommand.Execute(null);
        page.ExtendedDays = 90;

        await page.ExtendedPauseCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialog.ConfirmCount);
        Assert.Equal(1, _coordinator.PauseCount);
        Assert.Equal(90, _coordinator.LastPauseDays);
        Assert.True(_coordinator.LastPauseExtended);
    }

    [Fact]
    public async Task An_extended_pause_aborts_when_declined()
    {
        _dialog.ConfirmResult = false;
        UpdatesViewModel page = Build();
        page.RefreshCommand.Execute(null);

        await page.ExtendedPauseCommand.ExecuteAsync(null);

        Assert.Equal(0, _coordinator.PauseCount);
    }

    [Fact]
    public async Task Resume_resumes_updates()
    {
        _coordinator.StatusToReturn = Status(paused: true, until: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        UpdatesViewModel page = Build();
        page.RefreshCommand.Execute(null);

        await page.ResumeCommand.ExecuteAsync(null);

        Assert.Equal(1, _coordinator.ResumeCount);
    }

    [Fact]
    public void The_end_of_servicing_warning_reflects_the_status()
    {
        _coordinator.StatusToReturn = Status(warning: true);
        UpdatesViewModel page = Build();

        page.RefreshCommand.Execute(null);

        Assert.True(page.EndOfServiceWarning);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdatesViewModel(null!, _dialog));
        Assert.Throws<ArgumentNullException>(() => new UpdatesViewModel(_coordinator, null!));
    }
}
