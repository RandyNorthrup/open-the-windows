using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Model;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

/// <summary>
/// One isolated negative test per structural rule id, plus the positive case.
/// Each test mutates the canonical <see cref="CatalogTestData.ValidEntry"/> so
/// exactly one rule fires. Schema-level rules (<c>schema</c>, <c>json</c>) are
/// covered by <see cref="CatalogLoaderTests"/>, since they need the loader.
/// </summary>
public sealed class CatalogValidatorTests
{
    private static void AssertOnlyRule(string rule, CatalogIssueSeverity severity, params TweakDefinition[] entries)
    {
        IReadOnlyList<CatalogIssue> issues = Validate(entries);
        CatalogIssue issue = Assert.Single(issues, i => string.Equals(i.Rule, rule, StringComparison.Ordinal));
        Assert.Equal(severity, issue.Severity);
    }

    [Fact]
    public void A_valid_entry_produces_no_issues()
    {
        Assert.Empty(Validate(ValidEntry()));
    }

    [Fact]
    public void Duplicate_id_reports_unique_id()
    {
        AssertOnlyRule("unique-id", CatalogIssueSeverity.Error, ValidEntry(), ValidEntry());
    }

    [Fact]
    public void Blank_title_reports_title_required()
    {
        AssertOnlyRule("title-required", CatalogIssueSeverity.Error, ValidEntry() with { Title = "   " });
    }

    [Fact]
    public void Overlong_title_reports_title_length()
    {
        AssertOnlyRule("title-length", CatalogIssueSeverity.Error, ValidEntry() with { Title = new string('x', 81) });
    }

    [Fact]
    public void Blank_description_reports_description_required()
    {
        AssertOnlyRule("description-required", CatalogIssueSeverity.Error, ValidEntry() with { Description = "" });
    }

    [Fact]
    public void Blank_rationale_reports_rationale_required()
    {
        AssertOnlyRule("rationale-required", CatalogIssueSeverity.Error, ValidEntry() with { Rationale = "" });
    }

    [Fact]
    public void Level_off_reports_level_off()
    {
        AssertOnlyRule("level-off", CatalogIssueSeverity.Error, ValidEntry() with { Level = Level.Off });
    }

    [Fact]
    public void Breaking_risk_below_strict_reports_breaking_level()
    {
        AssertOnlyRule("breaking-level", CatalogIssueSeverity.Error,
            ValidEntry() with { Risk = RiskTier.Breaking, Level = Level.Balanced, SideEffects = ["Breaks a workflow."] });
    }

    [Fact]
    public void Advanced_risk_at_basic_reports_advanced_level()
    {
        AssertOnlyRule("advanced-level", CatalogIssueSeverity.Error,
            ValidEntry() with { Risk = RiskTier.Advanced, Level = Level.Basic, SideEffects = ["Changes behaviour."] });
    }

    [Fact]
    public void Caution_without_side_effects_warns_side_effects_expected()
    {
        AssertOnlyRule("side-effects-expected", CatalogIssueSeverity.Warning,
            ValidEntry() with { Risk = RiskTier.Caution, SideEffects = [] });
    }

    [Fact]
    public void Self_dependency_reports_self_reference()
    {
        AssertOnlyRule("self-reference", CatalogIssueSeverity.Error,
            ValidEntry() with { DependsOn = [new TweakId("privacy.test.entry")] });
    }

    [Fact]
    public void Dangling_dependency_reports_unknown_reference()
    {
        AssertOnlyRule("unknown-reference", CatalogIssueSeverity.Error,
            ValidEntry() with { DependsOn = [new TweakId("does.not.exist")] });
    }

    [Fact]
    public void No_sources_reports_sources_required()
    {
        AssertOnlyRule("sources-required", CatalogIssueSeverity.Error, ValidEntry() with { Sources = [] });
    }

    [Fact]
    public void Non_https_source_reports_source_https()
    {
        AssertOnlyRule("source-https", CatalogIssueSeverity.Error,
            ValidEntry() with { Sources = [new Uri("http://example.com/insecure")] });
    }

    [Fact]
    public void Verified_without_records_reports_verified_evidence()
    {
        AssertOnlyRule("verified-evidence", CatalogIssueSeverity.Error,
            ValidEntry() with { Status = TweakStatus.Verified, VerifiedOn = [] });
    }

    [Fact]
    public void Pre_windows_11_verification_reports_verified_build()
    {
        AssertOnlyRule("verified-build", CatalogIssueSeverity.Error,
            ValidEntry() with { VerifiedOn = [new Verification(21000, WindowsEdition.Pro, new DateOnly(2026, 1, 1))] });
    }

    [Fact]
    public void No_actions_reports_actions_required()
    {
        AssertOnlyRule("actions-required", CatalogIssueSeverity.Error, ValidEntry() with { Actions = [] });
    }

    [Fact]
    public void Protected_service_reports_protected_service()
    {
        AssertOnlyRule("protected-service", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [new ServiceAction("wuauserv", ServiceStartType.Manual, ServiceStartType.Automatic, false)] });
    }

    [Fact]
    public void Non_allow_listed_executable_reports_command_allowlist()
    {
        AssertOnlyRule("command-allowlist", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [new CommandAction("evil.exe", ["/apply"], ["/revert"])] });
    }

    [Fact]
    public void Command_without_revert_reports_revert_required()
    {
        AssertOnlyRule("revert-required", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [new CommandAction("powercfg.exe", [], [])] });
    }

    [Fact]
    public void Task_path_without_leading_backslash_reports_task_path()
    {
        AssertOnlyRule("task-path", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [new ScheduledTaskAction(@"Microsoft\Windows\Foo", false, true)] });
    }

    [Fact]
    public void Appx_package_name_reports_appx_family_name()
    {
        AssertOnlyRule("appx-family-name", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [new AppxAction("MicrosoftTeams", AppxRemovalMode.CurrentUser, "Reinstall from the Store.")] });
    }

    [Fact]
    public void Registry_path_with_traversal_reports_registry_path()
    {
        AssertOnlyRule("registry-path", CatalogIssueSeverity.Error,
            ValidEntry() with
            {
                Actions =
                [
                    new RegistryAction(RegistryHive.LocalMachine, @"SOFTWARE\Foo\..\Bar", "Name",
                        RegistryValueType.Dword, Number(1), Number(0), RegistryValueKind.Preference),
                ],
            });
    }

    [Fact]
    public void User_hive_with_machine_scope_reports_scope_hive()
    {
        AssertOnlyRule("scope-hive", CatalogIssueSeverity.Error,
            ValidEntry() with
            {
                Scope = Scope.Machine,
                Actions =
                [
                    new RegistryAction(RegistryHive.User, @"SOFTWARE\Foo", "Name",
                        RegistryValueType.Dword, Number(1), Number(0), RegistryValueKind.Preference),
                ],
            });
    }

    [Fact]
    public void Policy_value_outside_policies_key_warns_policy_path()
    {
        AssertOnlyRule("policy-path", CatalogIssueSeverity.Warning,
            ValidEntry() with
            {
                Actions =
                [
                    new RegistryAction(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Foo", "Name",
                        RegistryValueType.Dword, Number(1), Number(0), RegistryValueKind.Policy),
                ],
            });
    }

    [Fact]
    public void Missing_default_reports_default_required()
    {
        AssertOnlyRule("default-required", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [ValidRegistry() with { Default = Absent }] });
    }

    [Fact]
    public void Value_type_mismatch_reports_registry_value_type()
    {
        AssertOnlyRule("registry-value-type", CatalogIssueSeverity.Error,
            ValidEntry() with { Actions = [ValidRegistry() with { Value = Text("not a number") }] });
    }
}
