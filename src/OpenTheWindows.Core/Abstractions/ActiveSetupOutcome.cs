namespace OpenTheWindows.Core.Abstractions;

/// <summary>What an <see cref="IActiveSetupRegistrar.Register"/> call did.</summary>
public enum ActiveSetupOutcome
{
    /// <summary>The component key was written (or its version bumped) under HKLM Active Setup.</summary>
    Registered = 0,

    /// <summary>
    /// Nothing was written because Windows setup / OOBE is still in progress; registering
    /// a per-user logon stub while an image is being generalised is never correct.
    /// </summary>
    SkippedDuringOobe = 1,
}
