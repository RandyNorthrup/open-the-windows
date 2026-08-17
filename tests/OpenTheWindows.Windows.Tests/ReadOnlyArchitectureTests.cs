namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Architectural guards for the OS-touching layer in M3, where writers now
/// exist: the readers must stay read-only, and the banned <c>Process</c> type
/// may be hosted only by the guarded command runner. Both scan the real source
/// tree so an accidental write or an out-of-bounds process host fails the build.
/// </summary>
public sealed class ReadOnlyArchitectureTests
{
    private static readonly string? RepoRoot = FindRepoRoot();

    // Mutating members that must never appear in a reader source file.
    private static readonly string[] ForbiddenInReaders =
    [
        ".SetValue(", ".DeleteValue(", ".DeleteSubKey", ".CreateSubKey", ".CreateSubKeyTree",
        "ChangeServiceConfig", "RemovePackageAsync", "DeprovisionPackage", "ProvisionPackage",
        ".WriteAllText", ".AppendAllText", "SetEnabled", "SetStartType", "SetPreference",
    ];

    [Fact]
    public void Reader_sources_reference_no_mutating_apis()
    {
        string? root = RepoRoot;
        if (root is null)
        {
            Assert.Skip("Runs only inside a source checkout (these tests scan the repository's source tree).");
            return;
        }

        string readers = Path.Combine(root, "src", "OpenTheWindows.Windows", "Readers");
        var violations = new List<string>();

        foreach (string file in Directory.EnumerateFiles(readers, "*.cs"))
        {
            string text = File.ReadAllText(file);
            foreach (string token in ForbiddenInReaders)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add(string.Concat(Path.GetFileName(file), ": ", token));
                }
            }
        }

        Assert.True(violations.Count == 0, "Reader references a mutating API: " + string.Join(", ", violations));
    }

    [Fact]
    public void Only_the_command_runner_suppresses_the_process_ban()
    {
        string? root = RepoRoot;
        if (root is null)
        {
            Assert.Skip("Runs only inside a source checkout (these tests scan the repository's source tree).");
            return;
        }

        string source = Path.Combine(root, "src");
        var offenders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), "WindowsCommandRunner.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (File.ReadAllText(file).Contains("RS0030", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, "The Process ban is suppressed outside the command runner in: " + string.Join(", ", offenders));
    }

    private static string? FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenTheWindows.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
