namespace OpenTheWindows.Core.Abstractions;

/// <summary>The outcome of registering one Active Setup component.</summary>
/// <param name="Outcome">Whether the component was written or skipped (OOBE).</param>
/// <param name="Version">
/// The <c>Version</c> value written (a monotonically increasing integer as a
/// string), or <see langword="null"/> when nothing was written.
/// </param>
/// <param name="ComponentKey">The component subkey name that was targeted.</param>
public sealed record ActiveSetupResult(ActiveSetupOutcome Outcome, string? Version, string ComponentKey);
