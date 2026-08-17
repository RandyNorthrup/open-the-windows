using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Tests.Catalog;

/// <summary>
/// Builders for catalogue unit tests. <see cref="ValidEntry"/> is a canonical
/// entry that passes every structural rule; each rule test mutates one field of
/// it with a <c>with</c> expression so the assertion isolates a single rule.
/// </summary>
internal static class CatalogTestData
{
    /// <summary>A JSON number value (for registry Dword/Qword data).</summary>
    public static JsonElement Number(int value) => JsonSerializer.SerializeToElement(value);

    /// <summary>A JSON string value (for registry Sz/ExpandSz/Binary data).</summary>
    public static JsonElement Text(string value) => JsonSerializer.SerializeToElement(value);

    /// <summary>The absent value (<see cref="JsonValueKind.Undefined"/>), i.e. no <c>default</c> declared.</summary>
    public static JsonElement Absent => default;

    /// <summary>A registry action that satisfies every registry rule.</summary>
    public static RegistryAction ValidRegistry() => new(
        RegistryHive.LocalMachine,
        @"SOFTWARE\Policies\OpenTheWindows\Test",
        "Sample",
        RegistryValueType.Dword,
        Number(1),
        Number(0),
        RegistryValueKind.Policy);

    /// <summary>An entry that produces no validation issues.</summary>
    public static TweakDefinition ValidEntry(string id = "privacy.test.entry") => new(
        new TweakId(id),
        "Test entry title",
        "A test entry description.",
        "Because tests need a rationale.",
        Category.Privacy,
        Level.Balanced,
        RiskTier.Safe,
        Scope.Machine,
        TweakStatus.Draft,
        Applicability.Any,
        [ValidRegistry()],
        RestartRequirement.None,
        [],
        [],
        null,
        [],
        [new Uri("https://learn.microsoft.com/windows/test")],
        [],
        []);

    /// <summary>Runs the structural validator over the given entries.</summary>
    public static IReadOnlyList<CatalogIssue> Validate(params TweakDefinition[] entries)
        => CatalogValidator.Validate(entries, _ => "test.json");

    /// <summary>A schema- and rule-valid tweak document body for one entry (JSON text).</summary>
    public static string EntryJson(string id, string title = "An entry title") => $$"""
    {
      "id": "{{id}}",
      "title": "{{title}}",
      "description": "A description.",
      "rationale": "A rationale.",
      "category": "Privacy",
      "level": "Basic",
      "risk": "Safe",
      "scope": "Machine",
      "status": "Draft",
      "appliesTo": { "editions": [], "minBuild": null, "maxBuild": null, "architectures": [] },
      "actions": [
        {
          "kind": "Registry",
          "hive": "LocalMachine",
          "path": "SOFTWARE\\Policies\\OpenTheWindows\\Test",
          "name": "Sample",
          "type": "Dword",
          "value": 1,
          "default": 0,
          "valueKind": "Policy"
        }
      ],
      "requires": "None",
      "dependsOn": [],
      "conflictsWith": [],
      "managedBy": null,
      "sideEffects": [],
      "sources": ["https://learn.microsoft.com/windows/test"],
      "verifiedOn": [],
      "tags": []
    }
    """;

    /// <summary>Wraps entry bodies into one catalogue document (JSON text).</summary>
    public static string DocumentJson(params string[] entries) => $$"""
    { "entries": [ {{string.Join(",", entries)}} ] }
    """;
}
