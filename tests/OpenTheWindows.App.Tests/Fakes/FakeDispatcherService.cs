using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>
/// Synchronous <see cref="IDispatcherService"/> for view-model tests: runs every
/// posted or invoked action inline on the calling thread so assertions can run
/// immediately after an apply/progress call.
/// </summary>
internal sealed class FakeDispatcherService : IDispatcherService
{
    /// <inheritdoc />
    public bool HasAccess => true;

    /// <summary>How many times <see cref="Post"/> has been called.</summary>
    public int PostCount { get; private set; }

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        PostCount++;
        action();
    }

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Task.CompletedTask;
    }
}
