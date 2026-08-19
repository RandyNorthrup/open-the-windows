using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The Profiles page: built-in list, apply, export and import with validation and signing state.</summary>
public sealed class ProfilesViewModelTests
{
    private static readonly TweakCatalog Catalog = CatalogLoader.LoadBuiltIn().Catalog!;
    private static readonly OperatingSystemFacts Facts = FakeOperatingSystemInfo.Windows11Pro24H2();

    private readonly FakeApplyFlowLauncher _launcher = new();
    private readonly FakeFileDialogService _fileDialog = new();

    private ProfilesViewModel Build() => new(Catalog, Facts, _launcher, _fileDialog);

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
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(null!, Facts, _launcher, _fileDialog));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, null!, _fileDialog));
        Assert.Throws<ArgumentNullException>(() => new ProfilesViewModel(Catalog, Facts, _launcher, null!));
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
}
