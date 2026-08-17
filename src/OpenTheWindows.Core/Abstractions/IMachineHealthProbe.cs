namespace OpenTheWindows.Core.Abstractions;

/// <summary>Runs the read-only machine health checks (Secure Boot, TPM, Defender, BitLocker, etc.).</summary>
public interface IMachineHealthProbe
{
    /// <summary>Runs every health check and returns the results in a stable order.</summary>
    IReadOnlyList<HealthCheckResult> Run();
}
