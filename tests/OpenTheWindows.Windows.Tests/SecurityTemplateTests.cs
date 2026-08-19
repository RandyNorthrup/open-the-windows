using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The <c>secedit</c> security-template parser and builder. Pure text logic, so it
/// is exercised directly without running <c>secedit</c>; the round-trip
/// (build a template, parse it back) is the key property.
/// </summary>
public sealed class SecurityTemplateTests
{
    private const string Export =
        "[Unicode]\r\nUnicode=yes\r\n[System Access]\r\n" +
        "MinimumPasswordLength = 14\r\nPasswordComplexity = 1\r\nLockoutBadCount = 5\r\n" +
        "[Event Audit]\r\nAuditSystemEvents = 0\r\n";

    [Fact]
    public void ReadSetting_returns_the_value_within_the_section()
    {
        Assert.Equal("14", SecurityTemplate.ReadSetting(Export, SecurityPolicySection.SystemAccess, "MinimumPasswordLength"));
        Assert.Equal("1", SecurityTemplate.ReadSetting(Export, SecurityPolicySection.SystemAccess, "PasswordComplexity"));
    }

    [Fact]
    public void ReadSetting_matches_the_key_case_insensitively()
        => Assert.Equal("5", SecurityTemplate.ReadSetting(Export, SecurityPolicySection.SystemAccess, "lockoutbadcount"));

    [Fact]
    public void ReadSetting_returns_null_when_the_setting_is_absent()
        => Assert.Null(SecurityTemplate.ReadSetting(Export, SecurityPolicySection.SystemAccess, "MaximumPasswordAge"));

    [Fact]
    public void ReadSetting_does_not_read_a_setting_from_another_section()
        => Assert.Null(SecurityTemplate.ReadSetting(Export, SecurityPolicySection.SystemAccess, "AuditSystemEvents"));

    [Fact]
    public void BuildMinimal_emits_a_template_that_round_trips_through_the_parser()
    {
        string inf = SecurityTemplate.BuildMinimal(SecurityPolicySection.SystemAccess, "MinimumPasswordLength", "14");

        Assert.Contains("signature=\"$CHICAGO$\"", inf, StringComparison.Ordinal);
        Assert.Contains("[System Access]", inf, StringComparison.Ordinal);
        Assert.Equal("14", SecurityTemplate.ReadSetting(inf, SecurityPolicySection.SystemAccess, "MinimumPasswordLength"));
    }
}
