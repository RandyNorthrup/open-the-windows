using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli;

/// <summary>
/// The CLI's safeguards for privileged writes and trust decisions, grouped so tests
/// can replace them together.
/// </summary>
/// <param name="CreateTrustedKeys">Builds the machine trust store of profile/catalogue signing keys.</param>
/// <param name="DataDirectories">Prepares folders a privileged run writes into (the hardened product data folder).</param>
/// <param name="ExecutableGuard">Refuses to register an executable a standard user could tamper with.</param>
/// <param name="CreateUserScopeApplyEngine">Builds the unelevated per-user apply engine (per-user journal, audit and lock).</param>
/// <param name="ProfileStagingDirectory">
/// The protected folder a profile file is copied into before a SYSTEM task or the
/// per-user logon fallback is pointed at it, so neither ever reads a file a standard
/// user can edit.
/// </param>
internal sealed record CliHardening(
    Func<ITrustedKeySource> CreateTrustedKeys,
    ISecureDirectory DataDirectories,
    IExecutableLocationGuard ExecutableGuard,
    Func<ApplyEngine> CreateUserScopeApplyEngine,
    string ProfileStagingDirectory);
