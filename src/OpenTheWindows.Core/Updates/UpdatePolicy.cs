using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Machine policy for the Windows Update guardrails, read from
/// <c>HKLM\SOFTWARE\Policies\OpenTheWindows</c> (set by the product's ADMX or Intune).
/// </summary>
public static class UpdatePolicy
{
    /// <summary>
    /// The <c>MaxExtendedPauseDays</c> value name (REG_DWORD): the longest extended pause an
    /// administrator allows, between the 35-day standard cap and 365 days.
    /// </summary>
    public const string MaxExtendedPauseDaysValueName = "MaxExtendedPauseDays";

    /// <summary>
    /// The extended-pause ceiling: the policy value clamped to
    /// [<see cref="PauseCalculator.DefaultMaxDays"/>, <see cref="PauseCalculator.DefaultExtendedCeilingDays"/>],
    /// or the default ceiling when the policy is not set.
    /// </summary>
    public static int ExtendedCeilingDays(IRegistryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        RegistryValueSnapshot snapshot = reader.Read(
            RegistryHive.LocalMachine, userSid: null, ProfilePolicy.PolicyKeyPath, MaxExtendedPauseDaysValueName);
        if (snapshot is not { Exists: true, Data: { ValueKind: JsonValueKind.Number } data } || !data.TryGetInt32(out int days))
        {
            return PauseCalculator.DefaultExtendedCeilingDays;
        }

        return Math.Clamp(days, PauseCalculator.DefaultMaxDays, PauseCalculator.DefaultExtendedCeilingDays);
    }
}
