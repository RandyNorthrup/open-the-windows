using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Core.Startup;

/// <summary>
/// Lists the apps Windows starts at sign-in and turns them on or off with the same switch
/// Task Manager and Settings use (<see cref="StartupApproved"/>). A change goes through the
/// apply engine as a one-entry run under the <see cref="ProfileName"/> profile, so it is
/// journaled, verified, audited and revertable from History like any other change. The Run
/// value or shortcut itself is never touched (the Run keys are a protected location).
/// </summary>
public sealed class StartupAppsService
{
    /// <summary>The profile name startup changes are journaled under.</summary>
    public const string ProfileName = "startup-apps";

    private const string IdPrefix = "startup.app.";
    private const int IdHashBytes = 8;

    private static readonly Uri[] Sources = [new("https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys")];

    private readonly IStartupSource _source;
    private readonly IRegistryReader _registry;
    private readonly IInteractiveUserResolver _users;
    private readonly ApplyEngine _engine;
    private readonly TimeProvider _time;

    /// <summary>Creates the service over the startup source, the registry, the interactive-user resolver, the engine and the clock.</summary>
    public StartupAppsService(IStartupSource source, IRegistryReader registry, IInteractiveUserResolver users, ApplyEngine engine, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(time);
        _source = source;
        _registry = registry;
        _users = users;
        _engine = engine;
        _time = time;
    }

    /// <summary>The machine-wide startup apps and the signed-in user's, by name, each with its switch state.</summary>
    public IReadOnlyList<StartupApp> List()
    {
        string? sid = _users.Resolve()?.Sid;
        return [.. _source.List(sid)
            .Select(entry => new StartupApp(entry.Location, entry.Name, entry.Command, IsEnabled(entry, sid)))
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(app => app.Location)];
    }

    /// <summary>Turns <paramref name="app"/> on or off at sign-in (a journaled, revertable run).</summary>
    public ApplyResult SetEnabled(StartupApp app, bool enabled, bool whatIf)
    {
        ArgumentNullException.ThrowIfNull(app);
        TweakDefinition entry = EntryFor(app, enabled, _time.GetUtcNow());
        return _engine.Apply([entry], new ApplyOptions(whatIf, false, false, false, false, ProfileName, false));
    }

    /// <summary>The stable entry id for an app's startup switch (location and name, hashed to fit the id grammar).</summary>
    public static TweakId IdFor(StartupApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{app.Location}|{app.Name.ToUpperInvariant()}")));
        return new TweakId(IdPrefix + Convert.ToHexStringLower(hash.AsSpan(0, IdHashBytes)));
    }

    private bool IsEnabled(StartupEntry entry, string? sid)
    {
        (RegistryHive hive, string path) = StartupApproved.KeyFor(entry.Location);
        return StartupApproved.IsEnabled(_registry.Read(hive, hive == RegistryHive.User ? sid : null, path, entry.Name));
    }

    private static TweakDefinition EntryFor(StartupApp app, bool enabled, DateTimeOffset at)
    {
        (RegistryHive hive, string path) = StartupApproved.KeyFor(app.Location);
        var action = new RegistryAction(
            hive, path, app.Name, RegistryValueType.Binary,
            UpdateJson.String(Convert.ToBase64String(StartupApproved.Value(enabled, at))), UpdateJson.Null, RegistryValueKind.Preference);
        string verb = enabled ? "Start" : "Do not start";
        return new TweakDefinition(
            IdFor(app),
            string.Create(CultureInfo.InvariantCulture, $"{verb} “{app.Name}” at sign-in"),
            string.Create(CultureInfo.InvariantCulture, $"Sets the startup switch Task Manager and Settings use for “{app.Name}” ({app.Command})."),
            "Apps that start with Windows slow sign-in and keep running in the background. The switch leaves the app installed and registered, so it can be turned back on.",
            Category.Performance,
            Level.Basic,
            enabled ? RiskTier.Safe : RiskTier.Caution,
            app.IsPerUser ? Scope.User : Scope.Machine,
            TweakStatus.Verified,
            Applicability.Any,
            [action],
            RestartRequirement.SignOut,
            [],
            [],
            null,
            enabled ? [] : ["The app no longer starts by itself at sign-in; start it from the Start menu when you need it."],
            Sources,
            [],
            ["startup"]);
    }
}
