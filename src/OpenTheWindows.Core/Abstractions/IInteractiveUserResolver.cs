namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Resolves the interactive session user via the WTS session APIs. Returns the
/// logged-on user, never the elevating process's identity.
/// </summary>
public interface IInteractiveUserResolver
{
    /// <summary>Returns the interactive user, or <see langword="null"/> when no interactive session exists.</summary>
    InteractiveUser? Resolve();
}
