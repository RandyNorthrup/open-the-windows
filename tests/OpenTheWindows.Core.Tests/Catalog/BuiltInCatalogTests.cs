using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class BuiltInCatalogTests
{
    private static TweakCatalog Load()
    {
        CatalogLoadResult result = CatalogLoader.LoadBuiltIn();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        return result.Catalog!;
    }

    [Fact]
    public void Built_in_catalogue_loads_without_errors()
    {
        CatalogLoadResult result = CatalogLoader.LoadBuiltIn();

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Catalog);
    }

    [Fact]
    public void Built_in_catalogue_has_at_least_twenty_entries()
    {
        Assert.True(Load().Count >= 20, "The built-in catalogue should ship at least 20 entries.");
    }

    [Theory]
    [InlineData(Category.Security, 120)]
    [InlineData(Category.Privacy, 90)]
    [InlineData(Category.Debloat, 60)]
    [InlineData(Category.Performance, 40)]
    [InlineData(Category.Shell, 30)]
    [InlineData(Category.Updates, 25)]
    public void Category_meets_its_M4_minimum(Category category, int minimum)
    {
        int actual = Load().InCategory(category).Count();

        Assert.True(actual >= minimum,
            string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"M4 requires at least {minimum} {category} entries; found {actual}."));
    }

    [Fact]
    public void Every_category_is_represented()
    {
        TweakCatalog catalog = Load();

        foreach (Category category in Enum.GetValues<Category>())
        {
            Assert.NotEmpty(catalog.InCategory(category));
        }
    }

    [Fact]
    public void Entry_ids_are_unique()
    {
        TweakCatalog catalog = Load();

        int distinct = catalog.Entries.Select(e => e.Id).Distinct().Count();

        Assert.Equal(catalog.Count, distinct);
    }

    [Fact]
    public void Every_entry_has_at_least_one_https_source()
    {
        foreach (TweakDefinition entry in Load().Entries)
        {
            Assert.NotEmpty(entry.Sources);
            Assert.All(entry.Sources, source => Assert.Equal(Uri.UriSchemeHttps, source.Scheme));
        }
    }

    [Fact]
    public void Every_verified_entry_has_at_least_one_verification_record()
    {
        IEnumerable<TweakDefinition> verified = Load().Entries.Where(e => e.Status == TweakStatus.Verified);

        foreach (TweakDefinition entry in verified)
        {
            Assert.NotEmpty(entry.VerifiedOn);
        }
    }

    [Fact]
    public void Usoclient_is_on_the_command_allow_list()
        => Assert.Contains("usoclient.exe", CommandAction.AllowedExecutables, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Catalogue_meets_the_M4_total_minimum()
        => Assert.True(Load().Count >= 300, "M4 requires at least 300 catalogue entries.");

    [Fact]
    public void Baseline_and_stig_security_entries_declare_management()
    {
        foreach (TweakDefinition entry in Load().Entries.Where(e => e.Category == Category.Security))
        {
            bool framework = entry.Tags.Any(t =>
                string.Equals(t, "stig", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "baseline", StringComparison.OrdinalIgnoreCase));
            if (!framework)
            {
                continue;
            }

            bool managed = entry.ManagedBy is { } m
                && (!string.IsNullOrWhiteSpace(m.PolicyCsp) || !string.IsNullOrWhiteSpace(m.GroupPolicyPath));

            Assert.True(managed, string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{entry.Id.Value} is tagged baseline/stig but declares no Policy CSP or Group Policy path."));
        }
    }

    [Fact]
    public void No_entry_reconfigures_a_protected_service()
    {
        foreach (TweakDefinition entry in Load().Entries)
        {
            foreach (ServiceAction service in entry.Actions.OfType<ServiceAction>())
            {
                Assert.False(ProtectedServices.IsProtected(service.Name),
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"{entry.Id.Value} reconfigures the protected service '{service.Name}'."));
            }
        }
    }

    [Fact]
    public void Every_appx_entry_declares_a_reinstall_hint()
    {
        foreach (TweakDefinition entry in Load().Entries)
        {
            foreach (AppxAction appx in entry.Actions.OfType<AppxAction>())
            {
                Assert.False(string.IsNullOrWhiteSpace(appx.ReinstallHint),
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"{entry.Id.Value} removes '{appx.PackageFamilyName}' without a reinstallHint."));
            }
        }
    }

    [Fact]
    public void No_entry_touches_defender_signature_updates_or_disables_a_protection()
    {
        foreach (TweakDefinition entry in Load().Entries)
        {
            foreach (ITweakAction action in entry.Actions)
            {
                switch (action)
                {
                    case RegistryAction registry:
                        Assert.False(DefenderSafety.RefusesRegistry(registry),
                            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{entry.Id.Value} writes under Defender Signature Updates."));
                        break;
                    case DefenderPreferenceAction defender:
                        Assert.False(DefenderSafety.RefusesDefender(defender),
                            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{entry.Id.Value} disables a Defender protection."));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
