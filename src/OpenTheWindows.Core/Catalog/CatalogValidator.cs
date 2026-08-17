using System.Text.Json;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Structural rules that JSON Schema cannot express (cross-entry references,
/// value/type agreement, safety allow-lists, risk/level consistency). Each
/// rule has a stable id used in messages and tests.
/// </summary>
public static class CatalogValidator
{
    /// <summary>Longest title the UI can render on one line.</summary>
    public const int MaxTitleLength = 80;

    private const uint MaxDword = uint.MaxValue;

    /// <summary>Validates a set of parsed entries; <paramref name="sourceOf"/> maps an entry to its file name for messages.</summary>
    public static IReadOnlyList<CatalogIssue> Validate(
        IReadOnlyList<TweakDefinition> entries,
        Func<TweakDefinition, string> sourceOf)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(sourceOf);

        List<CatalogIssue> issues = [];
        HashSet<TweakId> ids = [.. entries.Select(e => e.Id)];

        foreach (TweakDefinition duplicate in entries.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Last()))
        {
            issues.Add(Error(sourceOf(duplicate), duplicate, "unique-id", $"Duplicate id '{duplicate.Id}'."));
        }

        foreach (TweakDefinition entry in entries)
        {
            string source = sourceOf(entry);
            ValidateText(entry, source, issues);
            ValidateLevelAndRisk(entry, source, issues);
            ValidateReferences(entry, source, ids, issues);
            ValidateSources(entry, source, issues);
            ValidateStatus(entry, source, issues);
            ValidateActions(entry, source, issues);
        }

        return issues;
    }

    private static void ValidateText(TweakDefinition entry, string source, List<CatalogIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(entry.Title))
        {
            issues.Add(Error(source, entry, "title-required", "Title is required."));
        }
        else if (entry.Title.Length > MaxTitleLength)
        {
            issues.Add(Error(source, entry, "title-length", $"Title is longer than {MaxTitleLength} characters."));
        }

        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            issues.Add(Error(source, entry, "description-required", "Description is required."));
        }

        if (string.IsNullOrWhiteSpace(entry.Rationale))
        {
            issues.Add(Error(source, entry, "rationale-required", "Rationale is required."));
        }
    }

    private static void ValidateLevelAndRisk(TweakDefinition entry, string source, List<CatalogIssue> issues)
    {
        if (entry.Level == Level.Off)
        {
            issues.Add(Error(source, entry, "level-off", "Level 'Off' is a profile dial position, not an entry level."));
        }

        // Built-in profiles never include Breaking tweaks, and Advanced tweaks
        // are never part of the zero-side-effect Basic level.
        if (entry.Risk == RiskTier.Breaking && entry.Level <= Level.Balanced)
        {
            issues.Add(Error(source, entry, "breaking-level", "Breaking-risk entries must be Strict or Paranoid."));
        }

        if (entry.Risk == RiskTier.Advanced && entry.Level == Level.Basic)
        {
            issues.Add(Error(source, entry, "advanced-level", "Advanced-risk entries cannot be Basic."));
        }

        if (entry.Risk >= RiskTier.Caution && entry.SideEffects.Count == 0)
        {
            issues.Add(Warning(source, entry, "side-effects-expected", "Caution or higher risk should list at least one side effect."));
        }
    }

    private static void ValidateReferences(TweakDefinition entry, string source, HashSet<TweakId> ids, List<CatalogIssue> issues)
    {
        foreach (TweakId dependency in entry.DependsOn)
        {
            if (dependency == entry.Id)
            {
                issues.Add(Error(source, entry, "self-reference", "An entry cannot depend on itself."));
            }
            else if (!ids.Contains(dependency))
            {
                issues.Add(Error(source, entry, "unknown-reference", $"dependsOn references unknown id '{dependency}'."));
            }
        }

        foreach (TweakId conflict in entry.ConflictsWith)
        {
            if (conflict == entry.Id)
            {
                issues.Add(Error(source, entry, "self-reference", "An entry cannot conflict with itself."));
            }
            else if (!ids.Contains(conflict))
            {
                issues.Add(Error(source, entry, "unknown-reference", $"conflictsWith references unknown id '{conflict}'."));
            }
        }
    }

    private static void ValidateSources(TweakDefinition entry, string source, List<CatalogIssue> issues)
    {
        if (entry.Sources.Count == 0)
        {
            issues.Add(Error(source, entry, "sources-required", "At least one documentation source URL is required."));
            return;
        }

        foreach (Uri uri in entry.Sources)
        {
            if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                issues.Add(Error(source, entry, "source-https", $"Source '{uri}' must be an absolute https URL."));
            }
        }
    }

    private static void ValidateStatus(TweakDefinition entry, string source, List<CatalogIssue> issues)
    {
        if (entry.Status == TweakStatus.Verified && entry.VerifiedOn.Count == 0)
        {
            issues.Add(Error(source, entry, "verified-evidence", "Verified entries need at least one verifiedOn record."));
        }

        foreach (Verification verification in entry.VerifiedOn)
        {
            if (verification.Build < Abstractions.OperatingSystemFacts.FirstWindows11Build)
            {
                issues.Add(Error(source, entry, "verified-build", $"verifiedOn build {verification.Build} is not a Windows 11 build."));
            }

            if (string.IsNullOrWhiteSpace(verification.Evidence))
            {
                issues.Add(Error(source, entry, "verified-evidence", "verifiedOn.evidence is required."));
            }
        }
    }

    private static void ValidateActions(TweakDefinition entry, string source, List<CatalogIssue> issues)
    {
        if (entry.Actions.Count == 0)
        {
            issues.Add(Error(source, entry, "actions-required", "At least one action is required."));
            return;
        }

        for (int index = 0; index < entry.Actions.Count; index++)
        {
            string location = $"{entry.Id}/actions/{index}";
            switch (entry.Actions[index])
            {
                case RegistryAction registry:
                    ValidateRegistry(entry, source, location, registry, issues);
                    break;
                case ServiceAction service:
                    if (ProtectedServices.IsProtected(service.Name))
                    {
                        issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "protected-service",
                            $"Service '{service.Name}' is protected and may not be reconfigured (PLAN.md D21)."));
                    }

                    break;
                case CommandAction command:
                    if (!CommandAction.AllowedExecutables.Contains(command.Executable))
                    {
                        issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "command-allowlist",
                            $"Executable '{command.Executable}' is not on the command allow-list."));
                    }

                    if (command.Arguments.Count == 0 || command.RevertArguments.Count == 0)
                    {
                        issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "revert-required",
                            "Command actions need both arguments and revertArguments."));
                    }

                    break;
                case ScheduledTaskAction task:
                    if (!task.Path.StartsWith('\\'))
                    {
                        issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "task-path",
                            "Scheduled task paths start with a backslash, e.g. \\Microsoft\\Windows\\..."));
                    }

                    break;
                case AppxAction appx:
                    if (!appx.PackageFamilyName.Contains('_', StringComparison.Ordinal))
                    {
                        issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "appx-family-name",
                            "packageFamilyName must be a family name (Name_PublisherId), not a package name."));
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static void ValidateRegistry(TweakDefinition entry, string source, string location, RegistryAction registry, List<CatalogIssue> issues)
    {
        if (registry.Path.StartsWith('\\') || registry.Path.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase)
            || registry.Path.Contains("..", StringComparison.Ordinal))
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "registry-path",
                "Registry path must be relative to the hive (no leading backslash, no HKEY_ prefix, no '..')."));
        }

        if (registry.Hive == RegistryHive.User && entry.Scope == Scope.Machine)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "scope-hive",
                "A user-hive action requires scope User or AllUsers."));
        }

        if (registry.Hive == RegistryHive.LocalMachine && entry.Scope != Scope.Machine)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "scope-hive",
                "A machine-hive action requires scope Machine."));
        }

        bool underPolicies = registry.Path.Contains(@"\Policies\", StringComparison.OrdinalIgnoreCase)
            || registry.Path.StartsWith(@"SOFTWARE\Policies\", StringComparison.OrdinalIgnoreCase);
        if (registry.ValueKind == RegistryValueKind.Policy && !underPolicies)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Warning, source, location, "policy-path",
                "Policy-kind values normally live under a Policies key."));
        }
        else if (registry.ValueKind == RegistryValueKind.Preference && underPolicies)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Warning, source, location, "policy-path",
                "Values under a Policies key should be declared as valueKind Policy."));
        }

        if (registry.Default.ValueKind == JsonValueKind.Undefined)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "default-required",
                "Registry actions must declare the Windows default ('default': value, or null when absent by default)."));
        }
        else if (registry.Default.ValueKind != JsonValueKind.Null && !ValueMatchesType(registry.Default, registry.Type))
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "registry-value-type",
                $"'default' does not match registry type {registry.Type}."));
        }

        if (registry.Value.ValueKind == JsonValueKind.Undefined || !ValueMatchesType(registry.Value, registry.Type))
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, location, "registry-value-type",
                $"'value' does not match registry type {registry.Type}."));
        }
    }

    /// <summary>Checks that a JSON value can be written as the given registry type.</summary>
    internal static bool ValueMatchesType(JsonElement value, RegistryValueType type)
        => type switch
        {
            RegistryValueType.Dword => value.ValueKind == JsonValueKind.Number
                && value.TryGetUInt64(out ulong dword) && dword <= MaxDword,
            RegistryValueType.Qword => value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out _),
            RegistryValueType.Sz or RegistryValueType.ExpandSz => value.ValueKind == JsonValueKind.String,
            RegistryValueType.MultiSz => value.ValueKind == JsonValueKind.Array && AllStrings(value),
            RegistryValueType.Binary => value.ValueKind == JsonValueKind.String && value.TryGetBytesFromBase64(out _),
            _ => false,
        };

    private static bool AllStrings(JsonElement array)
    {
        using JsonElement.ArrayEnumerator items = array.EnumerateArray();
        while (items.MoveNext())
        {
            if (items.Current.ValueKind != JsonValueKind.String)
            {
                return false;
            }
        }

        return true;
    }

    private static CatalogIssue Error(string source, TweakDefinition entry, string rule, string message)
        => new(CatalogIssueSeverity.Error, source, entry.Id.Value, rule, message);

    private static CatalogIssue Warning(string source, TweakDefinition entry, string rule, string message)
        => new(CatalogIssueSeverity.Warning, source, entry.Id.Value, rule, message);
}
