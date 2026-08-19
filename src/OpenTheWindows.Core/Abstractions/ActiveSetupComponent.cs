namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// One Active Setup component to register under
/// <c>HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components</c>. Windows runs
/// the <see cref="StubPath"/> in each user's own context at their next sign-in
/// whenever the machine-wide <c>Version</c> is newer than the user's copy — the
/// mechanism that reaches users who were logged off (or did not yet exist) when an
/// all-users apply ran. Built by <see cref="Engine.ActiveSetupFallback.For"/> so the
/// key name and stub command are validated and formatted in one place.
/// </summary>
/// <param name="ComponentKey">
/// The component subkey name, always of the form <c>{OTW-&lt;profileId&gt;}</c>; the
/// braces and <c>OTW-</c> prefix make the product's own components identifiable
/// (and removable) among the machine's other Active Setup entries.
/// </param>
/// <param name="DisplayName">The key's default value — a human-readable component name.</param>
/// <param name="StubPath">
/// The command Windows runs per user at sign-in, e.g.
/// <c>"C:\…\otw.exe" remediate --user-scope --profile &lt;id&gt;</c>. It runs
/// non-elevated in the signing-in user's session and writes only that user's hive.
/// </param>
public sealed record ActiveSetupComponent(string ComponentKey, string DisplayName, string StubPath);
