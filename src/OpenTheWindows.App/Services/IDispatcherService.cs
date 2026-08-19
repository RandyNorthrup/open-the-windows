namespace OpenTheWindows.App.Services;

/// <summary>
/// Marshals work onto the UI thread. Abstracted so view models can run
/// background work and post results back without referencing the live WPF
/// <see cref="System.Windows.Threading.Dispatcher"/>, which keeps them
/// testable off the UI thread.
/// </summary>
internal interface IDispatcherService
{
    /// <summary>True when the caller is already executing on the UI thread.</summary>
    bool HasAccess { get; }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread and returns
    /// immediately. Use for progress updates raised from a background thread.
    /// </summary>
    void Post(Action action);

    /// <summary>Runs <paramref name="action"/> on the UI thread and awaits its completion.</summary>
    Task InvokeAsync(Action action);
}
