using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// A read-only check a baseline rule uses when no catalogue tweak represents the
/// control (Secure Boot, TPM, BitLocker, VBS and the like). It names one machine
/// health check by its id; the audit engine reads that check's live result and
/// maps its status to the rule outcome. The referenced id must be one of
/// <see cref="HealthCheckIds.All"/> (enforced by <see cref="BaselineValidator"/>).
/// </summary>
/// <param name="HealthCheckId">The <see cref="HealthCheckIds"/> id to evaluate.</param>
public sealed record BaselineCheck(string HealthCheckId);
