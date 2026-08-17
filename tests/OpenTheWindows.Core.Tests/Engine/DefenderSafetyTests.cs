using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.TestSupport.Fakes;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Engine;

/// <summary>
/// Tests the Defender safety boundary: writes under
/// <c>Windows Defender\Signature Updates</c> and preferences that disable a
/// protection are refused by the engine and by the shared guard; enabling
/// protections is allowed.
/// </summary>
public sealed class DefenderSafetyTests
{
    private static readonly ApplyOptions Options = new(false, false, false, true, true, "balanced", false);

    private static RegistryAction SignatureUpdatesAction() => new(
        RegistryHive.LocalMachine,
        @"SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates",
        "ForceUpdateFromMU",
        RegistryValueType.Dword,
        Number(0),
        Number(0),
        RegistryValueKind.Policy);

    [Fact]
    public void Guard_refuses_signature_updates_registry_write()
        => Assert.True(DefenderSafety.RefusesRegistry(SignatureUpdatesAction()));

    [Fact]
    public void Guard_allows_other_defender_policy_registry_write()
    {
        var action = new RegistryAction(
            RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender", "PUAProtection",
            RegistryValueType.Dword, Number(1), Number(0), RegistryValueKind.Policy);

        Assert.False(DefenderSafety.RefusesRegistry(action));
    }

    [Fact]
    public void Guard_refuses_disabling_real_time_protection()
    {
        var action = new DefenderPreferenceAction("DisableRealtimeMonitoring", JsonSerializer.SerializeToElement(true), JsonSerializer.SerializeToElement(false));

        Assert.True(DefenderSafety.RefusesDefender(action));
    }

    [Fact]
    public void Guard_allows_enabling_a_defender_protection()
    {
        var action = new DefenderPreferenceAction("PUAProtection", Number(1), Number(0));

        Assert.False(DefenderSafety.RefusesDefender(action));
    }

    [Fact]
    public void Engine_refuses_and_audits_a_signature_updates_entry()
    {
        var machine = new FakeMachine();
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        TweakDefinition entry = ValidEntry("security.defender.signature-updates") with { Actions = [SignatureUpdatesAction()] };

        PlanResult plan = harness.Engine.Plan([entry], Options);
        Assert.Contains(plan.Entries, e => e.Disposition == PlanDisposition.Refused);

        ApplyResult result = harness.Engine.Apply([entry], Options);
        Assert.Contains(result.Entries, e => e.Result == ApplyState.Refused);
        Assert.True(harness.Audit.Has(AuditEventId.Refusal));
    }
}
