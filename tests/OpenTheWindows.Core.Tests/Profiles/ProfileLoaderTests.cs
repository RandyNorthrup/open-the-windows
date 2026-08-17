using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Profiles;

public sealed class ProfileLoaderTests
{
    private const string ValidProfileJson = """
    {
      "$schema": "https://github.com/RandyNorthrup/open-the-windows/catalog/schema/profile.schema.json",
      "schemaVersion": 1,
      "id": "unit-test",
      "name": "Unit Test",
      "description": "A profile loaded by a unit test.",
      "levels": { "Privacy": "Strict", "Updates": "Basic", "Security": "Balanced", "Performance": "Off", "Debloat": "Basic", "Shell": "Balanced" },
      "include": ["privacy.a.basic-safe"],
      "exclude": ["privacy.b.balanced-safe"],
      "includeDraft": false,
      "scope": "AllUsers",
      "options": { "restorePoint": true, "restartExplorer": false, "allowAdvanced": true, "allowBreaking": false },
      "appliesTo": { "editions": ["Pro"], "minBuild": 22631, "maxBuild": null, "architectures": ["x64"] }
    }
    """;

    [Fact]
    public void Valid_profile_loads_with_every_field()
    {
        ProfileLoadResult result = ProfileLoader.LoadText("unit-test.json", ValidProfileJson);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        Profile profile = result.Profile!;
        Assert.Equal("unit-test", profile.Id);
        Assert.Equal(Level.Strict, profile.LevelFor(Category.Privacy));
        Assert.Equal(Level.Off, profile.LevelFor(Category.Performance));
        Assert.Equal(Scope.AllUsers, profile.Scope);
        Assert.True(profile.Options.AllowAdvanced);
        Assert.Equal([new TweakId("privacy.a.basic-safe")], profile.Include);
        Assert.Equal([new TweakId("privacy.b.balanced-safe")], profile.Exclude);
        Assert.Equal([WindowsEdition.Pro], profile.AppliesTo.Editions);
        Assert.Equal(22631, profile.AppliesTo.MinBuild);
    }

    // One row per way a profile document is rejected: an unknown member, a
    // missing required field, an omitted category level (all caught by the JSON
    // Schema, rule "schema"), and an unsupported schemaVersion (caught after the
    // schema, rule "schema-version").
    [Theory]
    [InlineData("\"includeDraft\": false,", "\"includeDraft\": false, \"bogus\": 1,", "schema")]
    [InlineData("\"includeDraft\": false,", "", "schema")]
    [InlineData("\"Shell\": \"Balanced\" ", "", "schema")]
    [InlineData("\"schemaVersion\": 1,", "\"schemaVersion\": 2,", "schema-version")]
    public void A_mutated_profile_is_rejected(string find, string replacement, string expectedRule)
    {
        string json = ValidProfileJson.Replace(find, replacement, StringComparison.Ordinal);

        ProfileLoadResult result = ProfileLoader.LoadText("unit-test.json", json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.Rule, expectedRule, StringComparison.Ordinal));
    }

    [Fact]
    public void Malformed_json_is_reported_not_thrown()
    {
        ProfileLoadResult result = ProfileLoader.LoadText("unit-test.json", "{ not json ");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.Rule, "json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_file_is_reported_not_thrown()
    {
        ProfileLoadResult result = ProfileLoader.LoadFile(Path.Combine(Path.GetTempPath(), "otw-no-such-profile-xyz.json"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.Rule, "file-missing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_loaded_profile_round_trips_through_json()
    {
        Profile original = ProfileLoader.LoadText("unit-test.json", ValidProfileJson).Profile!;

        string json = JsonSerializer.Serialize(original, ProfileJsonContext.Default.Profile);
        Profile reloaded = ProfileLoader.LoadText("round-trip.json", json).Profile!;

        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(original.Scope, reloaded.Scope);
        Assert.Equal(original.Include, reloaded.Include);
        Assert.Equal(original.Exclude, reloaded.Exclude);
        foreach (Category category in Enum.GetValues<Category>())
        {
            Assert.Equal(original.LevelFor(category), reloaded.LevelFor(category));
        }
    }
}
