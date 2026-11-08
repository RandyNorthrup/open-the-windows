using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The Profiles page: built-in list, apply, export, import with validation and signing state, and capture.</summary>
public sealed class ProfilesViewModelTests
{
    private static readonly TweakCatalog Catalog = CatalogLoader.LoadBuiltIn().Catalog!;
    private static readonly OperatingSystemFacts Facts = FakeOperatingSystemInfo.Windows11Pro24H2();

    private readonly FakeApplyFlowLauncher _launcher = new();
    private readonly FakeFileDialogService _fileDialog = new();
    private readonly AppSettings _settings = new();
    private readonly FakeApplyCoordinator _coordinator = new();

    private readonly string _trustStore = Directory.CreateTempSubdirectory("otw-gui-trust").FullName;
    private ProfilePolicy _policy = ProfilePolicy.Unrestricted;

    private DirectoryTrustedKeySource Keys => new(_trustStore);

    private ProfilesViewModel Build() => new(Catalog, Facts, _launcher, _fileDialog, _settings, Keys, _policy, _coordinator);

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"otw-{Guid.NewGuid():N}.json");

    [Fact]
    public void Loads_the_built_in_profiles()
    {
        ProfilesViewModel page = Build();

        Assert.Equal(PageKeys.Profiles, page.Key);
        Assert.Equal(ProfileLoader.BuiltInIds.Count, page.Profiles.Count);
        Assert.All(page.Profiles, p => Assert.Equal(ProfileSignatureStatus.BuiltIn, p.Signature));
        Assert.All(page.Profiles, p => Assert.True(p.IsValid));
    }

    [Fact]
    public void Apply_is_gated_on_a_valid_selection_and_launches_the_flow()
    {
        ProfilesViewModel page = Build();
        Assert.False(page.ApplySelectedCommand.CanExecute(null));

        page.SelectedProfile = page.Profiles[0];
        Assert.True(page.ApplySelectedCommand.CanExecute(null));

        page.ApplySelectedCommand.Execute(null);
        Assert.Equal(1, _launcher.LaunchCount);
        Assert.Equal(page.Profiles[0].ResolvedEntries.Count, _launcher.LastEntries!.Count);
    }

    [Fact]
    public void Export_then_import_round_trips_a_profile_as_unsigned()
    {
        string path = TempPath();
        try
        {
            _fileDialog.SavePath = path;
            ProfilesViewModel page = Build();
            page.SelectedProfile = page.Profiles[0];

            page.ExportCommand.Execute(null);
            Assert.True(File.Exists(path));

            _fileDialog.OpenPath = path;
            int before = page.Profiles.Count;
            page.ImportCommand.Execute(null);

            Assert.Equal(before + 1, page.Profiles.Count);
            ProfileItemViewModel imported = page.Profiles[^1];
            Assert.Equal(ProfileSignatureStatus.Unsigned, imported.Signature);
            Assert.True(imported.IsValid);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_unreadable_signature_is_reported_invalid()
    {
        string path = TempPath();
        try
        {
            ExportFirstProfile(path);
            File.WriteAllText(path + ProfileTrust.SignatureSuffix, "not a signature document");

            ProfilesViewModel page = Reimport(path);

            Assert.Equal(ProfileSignatureStatus.Invalid, page.Profiles[^1].Signature);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ProfileTrust.SignatureSuffix);
        }
    }

    [Fact]
    public void A_signature_from_an_untrusted_key_is_reported_untrusted()
    {
        string path = TempPath();
        try
        {
            ExportFirstProfile(path);
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ProfileSignatureDocument doc = ProfileSignature.Sign(File.ReadAllText(path), key);
            File.WriteAllText(path + ProfileTrust.SignatureSuffix,
                JsonSerializer.Serialize(doc, ProfileJsonContext.Default.ProfileSignatureDocument));

            ProfilesViewModel page = Reimport(path);

            Assert.Equal(ProfileSignatureStatus.Untrusted, page.Profiles[^1].Signature);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ProfileTrust.SignatureSuffix);
        }
    }

    [Fact]
    public void A_cancelled_import_changes_nothing()
    {
        _fileDialog.OpenPath = null;
        ProfilesViewModel page = Build();
        int before = page.Profiles.Count;

        page.ImportCommand.Execute(null);

        Assert.Equal(before, page.Profiles.Count);
    }

    [Fact]
    public void Enterprise_mode_hides_the_consumer_profiles()
    {
        ProfilesViewModel page = Build();
        int all = page.Profiles.Count;

        _settings.EnterpriseMode = true;
        page.OnActivated();

        Assert.True(page.Profiles.Count < all);
        Assert.DoesNotContain(page.Profiles, p => string.Equals(p.Profile.Id, "home", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(null!, Facts, _launcher, _fileDialog, _settings, Keys, _policy, _coordinator));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, null!, _fileDialog, _settings, Keys, _policy, _coordinator));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, _launcher, null!, _settings, Keys, _policy, _coordinator));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, _launcher, _fileDialog, null!, Keys, _policy, _coordinator));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, _launcher, _fileDialog, _settings, null!, _policy, _coordinator));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, _launcher, _fileDialog, _settings, Keys, null!, _coordinator));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, _launcher, _fileDialog, _settings, Keys, _policy, null!));
    }

    [Fact]
    public void Under_the_signing_policy_an_unsigned_import_cannot_be_applied()
    {
        string path = TempPath();
        try
        {
            ExportFirstProfile(path);
            _policy = new ProfilePolicy(RequireSignedProfiles: true);

            ProfilesViewModel page = Reimport(path);

            Assert.Equal(ProfileSignatureStatus.Unsigned, page.Profiles[^1].Signature);
            Assert.False(page.CanApplySelected);
            Assert.False(page.ApplySelectedCommand.CanExecute(null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Under_the_signing_policy_a_trusted_import_can_be_applied()
    {
        string path = TempPath();
        try
        {
            ExportFirstProfile(path);
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(Path.Combine(_trustStore, "signer.pem"), key.ExportSubjectPublicKeyInfoPem());
            ProfileSignatureDocument doc = ProfileSignature.Sign(File.ReadAllText(path), key);
            File.WriteAllText(path + ProfileTrust.SignatureSuffix,
                JsonSerializer.Serialize(doc, ProfileJsonContext.Default.ProfileSignatureDocument));
            _policy = new ProfilePolicy(RequireSignedProfiles: true);

            ProfilesViewModel page = Reimport(path);

            Assert.Equal(ProfileSignatureStatus.Trusted, page.Profiles[^1].Signature);
            Assert.True(page.CanApplySelected);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ProfileTrust.SignatureSuffix);
        }
    }

    private void ExportFirstProfile(string path)
    {
        _fileDialog.SavePath = path;
        ProfilesViewModel exporter = Build();
        exporter.SelectedProfile = exporter.Profiles[0];
        exporter.ExportCommand.Execute(null);
    }

    private ProfilesViewModel Reimport(string path)
    {
        _fileDialog.OpenPath = path;
        ProfilesViewModel page = Build();
        page.ImportCommand.Execute(null);
        return page;
    }

    [Fact]
    public async Task Capture_scans_verified_entries_and_saves_a_loadable_profile_of_what_is_in_place()
    {
        TweakDefinition inPlace = Catalog.Entries.First(e => e.Status == TweakStatus.Verified && e.Risk == RiskTier.Safe && e.AppliesTo.Matches(Facts));
        TweakDefinition missing = Catalog.Entries.Last(e => e.Status == TweakStatus.Verified && e.Risk == RiskTier.Safe && e.AppliesTo.Matches(Facts));
        _coordinator.ScanToReturn = new ScanReport(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero), Facts, "capture",
        [
            new TweakObservation(inPlace.Id, ComplianceState.Compliant, inPlace.Risk, []),
            new TweakObservation(missing.Id, ComplianceState.Drift, missing.Risk, []),
        ]);
        string path = TempPath();
        _fileDialog.SavePath = path;
        ProfilesViewModel page = Build();

        try
        {
            await page.CaptureCommand.ExecuteAsync(null);

            Assert.All(_coordinator.LastScanned!, e => Assert.Equal(TweakStatus.Verified, e.Status));
            ProfileLoadResult loaded = ProfileLoader.LoadText(path, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.NotNull(loaded.Profile);
            Assert.Equal("captured-20261107", loaded.Profile.Id);
            Assert.Equal([inPlace.Id], loaded.Profile.Include);
            Assert.Contains("Captured 1 setting", page.Status, StringComparison.Ordinal);
            Assert.True(page.CanCapture);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Cancelling_the_capture_dialog_scans_nothing()
    {
        _fileDialog.SavePath = null;
        ProfilesViewModel page = Build();

        await page.CaptureCommand.ExecuteAsync(null);

        Assert.Null(_coordinator.LastScanned);
    }

    [Fact]
    public async Task A_capture_fault_is_reported()
    {
        _coordinator.ThrowOnScan = true;
        _fileDialog.SavePath = TempPath();
        ProfilesViewModel page = Build();

        await page.CaptureCommand.ExecuteAsync(null);

        Assert.Contains("Capture failed", page.Status, StringComparison.Ordinal);
        Assert.False(File.Exists(_fileDialog.SavePath));
    }
}
