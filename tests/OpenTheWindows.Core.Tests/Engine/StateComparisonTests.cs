using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Tests.Engine;

/// <summary>
/// Value-equality tests for every registry type and for arbitrary JSON, the
/// comparison shared by scan and apply-verify.
/// </summary>
public sealed class StateComparisonTests
{
    private static JsonElement E(object value) => JsonSerializer.SerializeToElement(value);

    [Theory]
    [InlineData(RegistryValueType.Dword)]
    [InlineData(RegistryValueType.Qword)]
    public void Numbers_compare_numerically(RegistryValueType type)
    {
        Assert.True(StateComparison.RegistryEquals(E(7), E(7), type));
        Assert.False(StateComparison.RegistryEquals(E(7), E(8), type));
        Assert.False(StateComparison.RegistryEquals(E("7"), E(7), type)); // kind mismatch
    }

    [Theory]
    [InlineData(RegistryValueType.Sz)]
    [InlineData(RegistryValueType.ExpandSz)]
    public void Strings_compare_ordinally(RegistryValueType type)
    {
        Assert.True(StateComparison.RegistryEquals(E("hi"), E("hi"), type));
        Assert.False(StateComparison.RegistryEquals(E("hi"), E("HI"), type));
        Assert.False(StateComparison.RegistryEquals(E(1), E("hi"), type));
    }

    [Fact]
    public void Multi_strings_compare_element_wise()
    {
        Assert.True(StateComparison.RegistryEquals(E(new[] { "a", "b" }), E(new[] { "a", "b" }), RegistryValueType.MultiSz));
        Assert.False(StateComparison.RegistryEquals(E(new[] { "a" }), E(new[] { "a", "b" }), RegistryValueType.MultiSz));
        Assert.False(StateComparison.RegistryEquals(E(new[] { "a", "b" }), E(new[] { "a", "c" }), RegistryValueType.MultiSz));
    }

    [Fact]
    public void Binary_compares_bytes()
    {
        JsonElement one = E(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        JsonElement same = E(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        JsonElement other = E(Convert.ToBase64String(new byte[] { 1, 2, 4 }));

        Assert.True(StateComparison.RegistryEquals(one, same, RegistryValueType.Binary));
        Assert.False(StateComparison.RegistryEquals(one, other, RegistryValueType.Binary));
        Assert.False(StateComparison.RegistryEquals(E(5), one, RegistryValueType.Binary));
    }

    [Fact]
    public void Appx_all_users_removal_treats_never_provisioned_absence_as_compliant()
    {
        // Absent everywhere and explicitly deprovisioned.
        Assert.True(StateComparison.AppxCompliant(new AppxSnapshot(false, false, false, true), AppxRemovalMode.AllUsersAndDeprovision));
        // Absent everywhere and never provisioned (cannot return) — also compliant.
        Assert.True(StateComparison.AppxCompliant(new AppxSnapshot(false, false, false, false), AppxRemovalMode.AllUsersAndDeprovision));
        // Still installed for some user — not compliant.
        Assert.False(StateComparison.AppxCompliant(new AppxSnapshot(false, true, true, false), AppxRemovalMode.AllUsersAndDeprovision));
    }

    [Fact]
    public void Appx_current_user_removal_checks_only_the_user()
    {
        Assert.True(StateComparison.AppxCompliant(new AppxSnapshot(false, true, true, false), AppxRemovalMode.CurrentUser));
        Assert.False(StateComparison.AppxCompliant(new AppxSnapshot(true, true, true, false), AppxRemovalMode.CurrentUser));
    }

    [Fact]
    public void Json_equals_handles_numbers_strings_bools_and_arrays()
    {
        Assert.True(StateComparison.JsonEquals(E(3), E(3)));
        Assert.False(StateComparison.JsonEquals(E(3), E(4)));
        Assert.True(StateComparison.JsonEquals(E("x"), E("x")));
        Assert.True(StateComparison.JsonEquals(E(true), E(true)));
        Assert.False(StateComparison.JsonEquals(E(true), E(false)));
        Assert.True(StateComparison.JsonEquals(E(new[] { 1, 2 }), E(new[] { 1, 2 })));
        Assert.False(StateComparison.JsonEquals(E(new[] { 1 }), E(new[] { 1, 2 })));
        Assert.False(StateComparison.JsonEquals(E(1), E("1"))); // kind mismatch
    }
}
