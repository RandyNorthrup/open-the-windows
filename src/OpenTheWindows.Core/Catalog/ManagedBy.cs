namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Where an organisation would manage the same setting, so the engine can
/// detect "managed by your organisation" and the UI can explain it.
/// </summary>
/// <param name="PolicyCsp">MDM Policy CSP path, e.g. <c>System/AllowTelemetry</c>.</param>
/// <param name="GroupPolicyPath">Group Policy editor path, e.g. <c>Computer Configuration\Administrative Templates\...</c>.</param>
public sealed record ManagedBy(string? PolicyCsp, string? GroupPolicyPath);
