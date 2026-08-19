using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw profile list|show|validate|sign|verify</c>: inspect, validate and sign
/// profiles (the built-in ones or an operator file). All read-only with respect
/// to the machine; <c>sign</c>/<c>verify</c> only touch the given files and the
/// trust store. Exit codes: 0 ok; 4 invalid input (unknown profile, unreadable
/// file, a profile that fails validation, or a signature that does not verify).
/// </summary>
internal static class ProfileCommand
{
    public static Command Create(Func<string?, CatalogLoadResult> loadCatalog)
    {
        ArgumentNullException.ThrowIfNull(loadCatalog);

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit JSON on stdout (stable schema).",
            Recursive = true,
        };

        var command = new Command("profile", "Inspect, validate and sign profiles.");
        command.Options.Add(jsonOption);
        command.Subcommands.Add(CreateList(jsonOption));
        command.Subcommands.Add(CreateShow(jsonOption));
        command.Subcommands.Add(CreateValidate(loadCatalog, jsonOption));
        command.Subcommands.Add(CreateSign());
        command.Subcommands.Add(CreateVerify());
        return command;
    }

    private static Command CreateList(Option<bool> json)
    {
        var command = new Command("list", "List the built-in profiles.");
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            List<Profile> profiles = [.. ProfileLoader.LoadBuiltIns()
                .Where(r => r.IsValid)
                .Select(r => r.Profile!)];

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize<IReadOnlyList<Profile>>(profiles, ProfileJsonContext.Default.IReadOnlyListProfile));
                return ExitCodes.Success;
            }

            WriteTable(stdout, profiles);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateShow(Option<bool> json)
    {
        var profileArgument = new Argument<string>("profile")
        {
            Description = "Built-in profile id (e.g. home) or a path to a profile .json file.",
        };
        var command = new Command("show", "Show one profile: its level dial, include/exclude, scope, options and applicability.");
        command.Arguments.Add(profileArgument);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            TextWriter stderr = parseResult.InvocationConfiguration.Error;
            string idOrPath = parseResult.GetValue(profileArgument) ?? string.Empty;

            if (!TryResolve(idOrPath, stderr, out ProfileLoadResult result, out _))
            {
                return ExitCodes.InvalidInput;
            }

            if (result.Profile is null)
            {
                WriteIssues(stderr, result.Errors);
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Profile '{idOrPath}' failed to load."));
                return ExitCodes.InvalidInput;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(result.Profile, ProfileJsonContext.Default.Profile));
                return ExitCodes.Success;
            }

            WriteProfile(stdout, result.Profile);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateValidate(Func<string?, CatalogLoadResult> loadCatalog, Option<bool> json)
    {
        var profileArgument = new Argument<string>("profile")
        {
            Description = "Built-in profile id (e.g. home) or a path to a profile .json file.",
        };
        var catalogDirOption = new Option<string?>("--catalog-dir")
        {
            Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
        };
        var command = new Command("validate", "Validate a profile against the schema and against the catalogue (id references, no built-in Breaking).");
        command.Arguments.Add(profileArgument);
        command.Options.Add(catalogDirOption);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            TextWriter stderr = parseResult.InvocationConfiguration.Error;
            string idOrPath = parseResult.GetValue(profileArgument) ?? string.Empty;

            if (!TryResolve(idOrPath, stderr, out ProfileLoadResult result, out bool isBuiltIn))
            {
                return ExitCodes.InvalidInput;
            }

            List<CatalogIssue> issues = [.. result.Issues];
            if (result.Profile is not null)
            {
                CatalogLoadResult catalog = loadCatalog(parseResult.GetValue(catalogDirOption));
                if (catalog.Catalog is null)
                {
                    WriteIssues(stderr, catalog.Errors);
                    stderr.WriteLine("Catalogue failed validation; run 'otw catalog validate' for details.");
                    return ExitCodes.InvalidInput;
                }

                issues.AddRange(ProfileValidator.Validate(result.Profile, catalog.Catalog, isBuiltIn));
            }

            bool valid = result.Profile is not null && !issues.Any(i => i.Severity == CatalogIssueSeverity.Error);
            int warnings = issues.Count(i => i.Severity == CatalogIssueSeverity.Warning);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize<IReadOnlyList<CatalogIssue>>(issues, CatalogJsonContext.Default.IReadOnlyListCatalogIssue));
                return valid ? ExitCodes.Success : ExitCodes.InvalidInput;
            }

            foreach (CatalogIssue issue in issues)
            {
                stdout.WriteLine(issue.ToString());
            }

            stdout.WriteLine(valid
                ? string.Create(CultureInfo.InvariantCulture, $"Profile '{idOrPath}' valid: {warnings} warning(s).")
                : string.Create(CultureInfo.InvariantCulture, $"Profile '{idOrPath}' INVALID: {issues.Count(i => i.Severity == CatalogIssueSeverity.Error)} error(s), {warnings} warning(s)."));
            return valid ? ExitCodes.Success : ExitCodes.InvalidInput;
        });
        return command;
    }

    private static Command CreateSign()
    {
        var profileArgument = new Argument<string>("profile") { Description = "Path to a profile .json file to sign." };
        var keyOption = new Option<string>("--key") { Description = "PEM file holding the ES256 (P-256) private key.", Required = true };
        var outOption = new Option<string?>("--out") { Description = "Where to write the detached signature (default: <profile>.sig)." };
        var command = new Command("sign", "Sign a profile file with an ES256 private key (writes a detached <profile>.sig).");
        command.Arguments.Add(profileArgument);
        command.Options.Add(keyOption);
        command.Options.Add(outOption);
        command.SetAction(parseResult => ExecuteSign(parseResult, profileArgument, keyOption, outOption));
        return command;
    }

    /// <summary>Sets up stdout/stderr and the profile path, checking the file exists; false (with a message) when it does not.</summary>
    private static bool TryBeginFileCommand(
        ParseResult parseResult, Argument<string> profileArgument, out TextWriter stdout, out TextWriter stderr, out string profilePath)
    {
        stdout = parseResult.InvocationConfiguration.Output;
        stderr = parseResult.InvocationConfiguration.Error;
        profilePath = parseResult.GetValue(profileArgument) ?? string.Empty;

        if (File.Exists(profilePath))
        {
            return true;
        }

        stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Profile file '{profilePath}' does not exist."));
        return false;
    }

    private static int ExecuteSign(ParseResult parseResult, Argument<string> profileArgument, Option<string> keyOption, Option<string?> outOption)
    {
        if (!TryBeginFileCommand(parseResult, profileArgument, out TextWriter stdout, out TextWriter stderr, out string profilePath))
        {
            return ExitCodes.InvalidInput;
        }

        ProfileLoadResult load = ProfileLoader.LoadFile(profilePath);
        if (load.Profile is null)
        {
            WriteIssues(stderr, load.Errors);
            stderr.WriteLine("Refusing to sign a profile that fails validation.");
            return ExitCodes.InvalidInput;
        }

        using ECDsa? key = CommandSupport.LoadEcKey(parseResult.GetValue(keyOption) ?? string.Empty, stderr);
        if (key is null)
        {
            return ExitCodes.InvalidInput;
        }

        ProfileSignatureDocument signature;
        try
        {
            signature = ProfileSignature.Sign(File.ReadAllText(profilePath), key);
        }
        catch (CryptographicException ex)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Could not sign with this key (does the PEM hold the private key?): {ex.Message}"));
            return ExitCodes.InvalidInput;
        }

        string sigPath = parseResult.GetValue(outOption) ?? (profilePath + ProfileTrust.SignatureSuffix);
        File.WriteAllText(sigPath, JsonSerializer.Serialize(signature, ProfileJsonContext.Default.ProfileSignatureDocument));
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Signed '{profilePath}' -> '{sigPath}' (keyId {signature.KeyId})."));
        return ExitCodes.Success;
    }

    private static Command CreateVerify()
    {
        var profileArgument = new Argument<string>("profile") { Description = "Path to a signed profile .json file." };
        var keyOption = new Option<string?>("--key") { Description = "PEM public key to verify against (default: the machine trust store)." };
        var sigOption = new Option<string?>("--sig") { Description = "Path to the detached signature (default: <profile>.sig)." };
        var command = new Command("verify", "Verify a profile's ES256 signature against a public key or the machine trust store.");
        command.Arguments.Add(profileArgument);
        command.Options.Add(keyOption);
        command.Options.Add(sigOption);
        command.SetAction(parseResult => ExecuteVerify(parseResult, profileArgument, keyOption, sigOption));
        return command;
    }

    private static int ExecuteVerify(ParseResult parseResult, Argument<string> profileArgument, Option<string?> keyOption, Option<string?> sigOption)
    {
        if (!TryBeginFileCommand(parseResult, profileArgument, out TextWriter stdout, out TextWriter stderr, out string profilePath))
        {
            return ExitCodes.InvalidInput;
        }

        string sigPath = parseResult.GetValue(sigOption) ?? (profilePath + ProfileTrust.SignatureSuffix);
        if (!File.Exists(sigPath))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"No signature file '{sigPath}'. Sign the profile first or pass --sig."));
            return ExitCodes.InvalidInput;
        }

        if (!TryReadSignature(sigPath, stderr, out ProfileSignatureDocument signature))
        {
            return ExitCodes.InvalidInput;
        }

        string profileJson = File.ReadAllText(profilePath);
        if (!TryVerify(profileJson, signature, parseResult.GetValue(keyOption), stderr, out bool verified))
        {
            return ExitCodes.InvalidInput;
        }

        if (verified)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Signature valid (alg {signature.Algorithm}, keyId {signature.KeyId})."));
            return ExitCodes.Success;
        }

        stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Signature INVALID for '{profilePath}'."));
        return ExitCodes.InvalidInput;
    }

    /// <summary>
    /// Evaluates the signature against either an explicit public key or the machine
    /// trust store. Returns <see langword="false"/> (having written a message) only
    /// when the verification could not be attempted (bad key file, empty trust
    /// store); otherwise returns <see langword="true"/> and sets
    /// <paramref name="verified"/> to the cryptographic result.
    /// </summary>
    private static bool TryVerify(string profileJson, ProfileSignatureDocument signature, string? keyPath, TextWriter stderr, out bool verified)
    {
        verified = false;
        if (keyPath is not null)
        {
            using ECDsa? key = CommandSupport.LoadEcKey(keyPath, stderr);
            if (key is null)
            {
                return false;
            }

            verified = ProfileSignature.Verify(profileJson, signature, key);
            return true;
        }

        string directory = ProfileTrust.DefaultTrustStoreDirectory;
        if (!Directory.Exists(directory) || !Directory.EnumerateFiles(directory, "*.pem").Any())
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"No public key given (--key) and no trusted keys in '{directory}'."));
            return false;
        }

        verified = ProfileTrust.VerifyAgainstTrustStore(profileJson, signature, directory);
        return true;
    }

    private static bool TryReadSignature(string path, TextWriter stderr, out ProfileSignatureDocument signature)
    {
        if (ProfileTrust.TryReadSignature(path, out signature))
        {
            return true;
        }

        stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Signature file '{path}' is not a valid signature document."));
        return false;
    }

    /// <summary>
    /// Resolves the argument to a profile source: an existing file first (explicit
    /// path), otherwise a built-in profile id. Reports the not-found case on stderr.
    /// </summary>
    private static bool TryResolve(string idOrPath, TextWriter stderr, out ProfileLoadResult result, out bool isBuiltIn)
    {
        if (File.Exists(idOrPath))
        {
            result = ProfileLoader.LoadFile(idOrPath);
            isBuiltIn = false;
            return true;
        }

        ProfileLoadResult? builtIn = ProfileLoader.TryLoadBuiltIn(idOrPath);
        if (builtIn is not null)
        {
            result = builtIn;
            isBuiltIn = true;
            return true;
        }

        stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"No built-in profile or profile file '{idOrPath}'. Run 'otw profile list' for the built-in ids."));
        result = null!;
        isBuiltIn = false;
        return false;
    }

    private static void WriteIssues(TextWriter writer, IEnumerable<CatalogIssue> issues)
    {
        foreach (CatalogIssue issue in issues)
        {
            writer.WriteLine(issue.ToString());
        }
    }

    private static void WriteTable(TextWriter writer, List<Profile> profiles)
    {
        int idWidth = Math.Max("ID".Length, profiles.Count == 0 ? 0 : profiles.Max(p => p.Id.Length));
        int nameWidth = Math.Max("NAME".Length, profiles.Count == 0 ? 0 : profiles.Max(p => p.Name.Length));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{"ID".PadRight(idWidth)}  {"NAME".PadRight(nameWidth)}  {"SCOPE",-9} DESCRIPTION"));
        foreach (Profile p in profiles)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{p.Id.PadRight(idWidth)}  {p.Name.PadRight(nameWidth)}  {p.Scope,-9} {p.Description}"));
        }

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{profiles.Count} profiles."));
    }

    private static void WriteProfile(TextWriter writer, Profile p)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{p.Id}  [{p.Name} / scope {p.Scope}]"));
        writer.WriteLine(p.Description);
        writer.WriteLine();

        writer.Write("Levels:");
        foreach (Category category in Enum.GetValues<Category>())
        {
            writer.Write(string.Create(CultureInfo.InvariantCulture, $" {category}={p.LevelFor(category)}"));
        }

        writer.WriteLine();
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Include: {(p.Include.Count == 0 ? "-" : string.Join(", ", p.Include.Select(i => i.Value)))}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Exclude: {(p.Exclude.Count == 0 ? "-" : string.Join(", ", p.Exclude.Select(e => e.Value)))}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Include draft: {p.IncludeDraft}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Options: restorePoint={p.Options.RestorePoint}, restartExplorer={p.Options.RestartExplorer}, allowAdvanced={p.Options.AllowAdvanced}, allowBreaking={p.Options.AllowBreaking}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Applies to: editions [{string.Join(", ", p.AppliesTo.Editions)}], builds {p.AppliesTo.MinBuild?.ToString(CultureInfo.InvariantCulture) ?? "*"}..{p.AppliesTo.MaxBuild?.ToString(CultureInfo.InvariantCulture) ?? "*"}, arch [{string.Join(", ", p.AppliesTo.Architectures)}]"));
    }
}
