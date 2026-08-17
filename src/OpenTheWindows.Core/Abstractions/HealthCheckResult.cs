namespace OpenTheWindows.Core.Abstractions;

/// <summary>Outcome of one read-only health check.</summary>
/// <param name="Id">Stable check id, e.g. <c>secure-boot</c>.</param>
/// <param name="Title">Human-readable title.</param>
/// <param name="Status">Pass / Warn / Fail / NotApplicable / Unknown.</param>
/// <param name="Detail">The observed value or explanation.</param>
/// <param name="Reference">Optional documentation link.</param>
public sealed record HealthCheckResult(string Id, string Title, HealthStatus Status, string Detail, Uri? Reference);
