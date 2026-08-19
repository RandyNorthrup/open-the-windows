using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// The state writers the apply engine uses to apply desired state and to restore
/// prior state during revert or rollback.
/// </summary>
/// <param name="Registry">Registry writer (policy-aware).</param>
/// <param name="Services">Service writer (refuses protected/PPL services).</param>
/// <param name="Tasks">Scheduled task writer.</param>
/// <param name="Appx">Appx package writer.</param>
/// <param name="Features">Optional feature writer.</param>
/// <param name="Defender">Defender preference writer.</param>
/// <param name="Power">Power setting writer.</param>
/// <param name="Commands">Allow-listed command runner.</param>
/// <param name="SecurityPolicy">Security-policy (secedit) writer.</param>
public sealed record StateWriters(
    IRegistryWriter Registry,
    IServiceWriter Services,
    IScheduledTaskWriter Tasks,
    IAppxWriter Appx,
    IOptionalFeatureWriter Features,
    IDefenderPreferenceWriter Defender,
    IPowerSettingWriter Power,
    ICommandRunner Commands,
    ISecurityPolicyWriter SecurityPolicy);
