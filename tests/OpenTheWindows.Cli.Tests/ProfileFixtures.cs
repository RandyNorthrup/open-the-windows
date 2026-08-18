namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// Shared profile JSON fixtures for CLI command tests. Kept in one place so the
/// full profile document is written exactly once (the duplication gate flags a
/// second hand-authored copy of the same structural block).
/// </summary>
internal static class ProfileFixtures
{
    /// <summary>A schema- and structurally-valid profile (id <c>custom-file</c>).</summary>
    public const string ValidProfile = """
    {
      "schemaVersion": 1,
      "id": "custom-file",
      "name": "Custom File",
      "description": "A profile loaded from a file by a CLI test.",
      "levels": { "Privacy": "Basic", "Updates": "Basic", "Security": "Basic", "Performance": "Basic", "Debloat": "Basic", "Shell": "Basic" },
      "include": [],
      "exclude": [],
      "includeDraft": false,
      "scope": "User",
      "options": { "restorePoint": true, "restartExplorer": true, "allowAdvanced": false, "allowBreaking": false },
      "appliesTo": { "editions": [], "minBuild": null, "maxBuild": null, "architectures": [] }
    }
    """;
}
