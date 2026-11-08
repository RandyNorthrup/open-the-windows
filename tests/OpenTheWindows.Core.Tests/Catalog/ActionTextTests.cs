using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Tests.Catalog;

/// <summary>Each catalogue action reads as one plain sentence (no record dump).</summary>
public sealed class ActionTextTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    public static TheoryData<ITweakAction, string> Actions() => new()
    {
        {
            new RegistryAction(RegistryHive.LocalMachine, @"SOFTWARE\Policies\X", "Allow", RegistryValueType.Dword, Json(0), Json(1), RegistryValueKind.Policy),
            @"Set HKLM\SOFTWARE\Policies\X\Allow to 0 (Dword policy)"
        },
        {
            new RegistryAction(RegistryHive.User, @"Software\X", string.Empty, RegistryValueType.Sz, Json("on"), Json("off"), RegistryValueKind.Preference),
            @"Set HKCU\Software\X\(default) to ""on"" (Sz setting)"
        },
        {
            new RegistryAction(RegistryHive.User, @"Software\X", "List", RegistryValueType.MultiSz, Json(new[] { "a", "b" }), Json(Array.Empty<string>()), RegistryValueKind.Preference),
            @"Set HKCU\Software\X\List to [""a"", ""b""] (MultiSz setting)"
        },
        { new ServiceAction("DiagTrack", ServiceStartType.Disabled, ServiceStartType.Automatic, StopIfRunning: true), "Set the DiagTrack service to start never (disabled), and stop it if it is running" },
        { new ServiceAction("Svc", ServiceStartType.AutomaticDelayed, ServiceStartType.Manual, StopIfRunning: false), "Set the Svc service to start automatically (delayed)" },
        { new ServiceAction("Svc", ServiceStartType.Manual, ServiceStartType.Automatic, StopIfRunning: false), "Set the Svc service to start manually" },
        { new ServiceAction("Svc", ServiceStartType.Automatic, ServiceStartType.Manual, StopIfRunning: false), "Set the Svc service to start automatically" },
        { new ScheduledTaskAction(@"\Microsoft\Windows\T", Enabled: false, Default: true), @"Disable the scheduled task \Microsoft\Windows\T" },
        { new ScheduledTaskAction(@"\T", Enabled: true, Default: false), @"Enable the scheduled task \T" },
        { new AppxAction("Some.App_x", AppxRemovalMode.AllUsersAndDeprovision, "Store"), "Remove the Some.App_x app (for every user, and not for new users either)" },
        { new AppxAction("Some.App_x", AppxRemovalMode.CurrentUser, "Store"), "Remove the Some.App_x app (for the signed-in user)" },
        { new OptionalFeatureAction("WMIC~~~~", Enabled: false, Default: true), "Turn off the Windows feature WMIC~~~~" },
        { new OptionalFeatureAction("Feature", Enabled: true, Default: false), "Turn on the Windows feature Feature" },
        { new DefenderPreferenceAction("PUAProtection", Json(1), Json(0)), "Set the Microsoft Defender preference PUAProtection to 1" },
        { new DefenderPreferenceAction("Flag", Json(true), Json(false)), "Set the Microsoft Defender preference Flag to true" },
        { new DefenderPreferenceAction("Flag", Json(false), Json(true)), "Set the Microsoft Defender preference Flag to false" },
        {
            new PowerSettingAction(Guid.Parse("2a737441-1930-4402-8d77-b2bebba308a3"), Guid.Parse("48e6b7a6-50f5-4782-a5d4-53bb50f7e73c"), 0, 1),
            "Set power setting 48e6b7a6-50f5-4782-a5d4-53bb50f7e73c to 0 when plugged in and 1 on battery"
        },
        { new SecurityPolicyAction(SecurityPolicySection.SystemAccess, "MinimumPasswordLength", "14", "0"), "Set the local security policy MinimumPasswordLength to 14" },
        { new CommandAction("auditpol.exe", ["/set", "/subcategory:Logon", "/success:enable"], []), "Run auditpol.exe /set /subcategory:Logon /success:enable" },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public void Each_action_reads_as_a_sentence(ITweakAction action, string expected)
        => Assert.Equal(expected, ActionText.Describe(action));

    [Fact]
    public void A_null_action_is_rejected()
        => Assert.Throws<ArgumentNullException>(() => ActionText.Describe(null!));
}
