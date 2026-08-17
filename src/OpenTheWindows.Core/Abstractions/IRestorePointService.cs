namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Creates a System Restore point and verifies it exists. Never overrides the
/// system restore-point frequency without an explicit force request; reports
/// <see cref="RestorePointStatus.Skipped24h"/> or
/// <see cref="RestorePointStatus.Disabled"/> instead of pretending to succeed.
/// </summary>
public interface IRestorePointService
{
    /// <summary>Requests a restore point described by <paramref name="description"/> and verifies the outcome.</summary>
    RestorePointResult Create(string description);
}
