using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IDispatcherService"/> over the live WPF <see cref="Dispatcher"/>.
/// The only production implementation; view-model tests use a synchronous fake.
/// This class is the single sanctioned boundary for UI-thread marshalling, which
/// is exactly why the VS-threading legacy-API rule is suppressed here (see below).
/// </summary>
[SuppressMessage(
    "Usage",
    "VSTHRD001:Avoid legacy thread switching APIs",
    Justification = "This type is the deliberate encapsulation of WPF Dispatcher marshalling for the whole app; " +
        "JoinableTaskFactory would add the VS-Threading package with no benefit for a standalone WPF process. " +
        "Recorded in PLAN.md section 7.3.")]
internal sealed class WpfDispatcherService : IDispatcherService
{
    private readonly Dispatcher _dispatcher;

    /// <summary>Creates the service over <paramref name="dispatcher"/> (the UI thread's dispatcher).</summary>
    public WpfDispatcherService(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public bool HasAccess => _dispatcher.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = _dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
    }

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
    }
}
