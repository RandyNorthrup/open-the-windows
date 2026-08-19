using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// The read-only state readers the apply engine uses to capture prior state and
/// verify applied state. The same instances back the <see cref="ScanEngine"/> so
/// scan and apply observe the machine identically.
/// </summary>
/// <param name="Registry">Registry value reader.</param>
/// <param name="Services">Service reader.</param>
/// <param name="Tasks">Scheduled task reader.</param>
/// <param name="Appx">Appx package reader.</param>
/// <param name="Features">Optional feature reader.</param>
/// <param name="Defender">Defender preference reader.</param>
/// <param name="Power">Power setting reader.</param>
/// <param name="SecurityPolicy">Security-policy (secedit) reader.</param>
public sealed record StateReaders(
    IRegistryReader Registry,
    IServiceReader Services,
    IScheduledTaskReader Tasks,
    IAppxReader Appx,
    IOptionalFeatureReader Features,
    IDefenderPreferenceReader Defender,
    IPowerSettingReader Power,
    ISecurityPolicyReader SecurityPolicy);
