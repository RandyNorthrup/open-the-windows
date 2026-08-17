using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Explorer-restart stub recording the sessions it was asked to restart.</summary>
public sealed class FakeExplorerRestarter : IExplorerRestarter
{
    /// <summary>The SIDs whose Explorer was asked to restart, in order.</summary>
    public IList<string> Restarts { get; } = [];

    /// <inheritdoc />
    public void RestartForSession(string userSid) => Restarts.Add(userSid);
}
