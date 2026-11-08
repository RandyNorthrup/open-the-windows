using System.Security.AccessControl;
using System.Security.Principal;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The privileged repair of the data folder, run elevated on the lab machine (skipped
/// unless <c>OTW_INTEGRATION=1</c>). The current account is treated as untrusted so the
/// files this test plants stand in for files a standard user planted.
/// </summary>
public sealed class SecureDataFolderIntegrationTests : IDisposable
{
    private static readonly SecurityIdentifier Me = WindowsIdentity.GetCurrent().User!;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "otw-repair-" + Guid.NewGuid().ToString("N"));
    private readonly string _elsewhere = Directory.CreateTempSubdirectory("otw-link-target").FullName;

    private static bool IsEnabled
        => string.Equals(Environment.GetEnvironmentVariable("OTW_INTEGRATION"), "1", StringComparison.Ordinal);

    private static bool DistrustMe(SecurityIdentifier sid) => sid != Me && TrustedPrincipals.IsTrusted(sid);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        Directory.Delete(_elsewhere, recursive: true);
    }

    [Fact]
    public void Repair_takes_ownership_locks_permissions_and_removes_planted_files_but_keeps_journals()
    {
        Assert.SkipUnless(IsEnabled, "Integration test: set OTW_INTEGRATION=1 (elevated, lab machine) to run.");
        string audit = Directory.CreateDirectory(Path.Combine(_root, "audit")).FullName;
        string journal = Directory.CreateDirectory(Path.Combine(_root, WindowsDataPaths.JournalFolderName)).FullName;
        GrantUsersModify(audit);
        PlantOwnedByMe(Path.Combine(audit, "otw-209912.jsonl"), "{\"forged\":true}");
        PlantOwnedByMe(Path.Combine(journal, "20990101-000000-" + Guid.NewGuid().ToString("D") + ".json"), "{}");
        var directories = new WindowsSecureDirectory(_root, privileged: true, DistrustMe);

        directories.EnsureSafeForWrite(audit);
        directories.EnsureSafeForWrite(journal);

        DirectorySecurity repaired = new DirectoryInfo(audit).GetAccessControl();
        Assert.Equal(TrustedPrincipals.Administrators, repaired.GetOwner(typeof(SecurityIdentifier)));
        Assert.True(repaired.AreAccessRulesProtected);
        Assert.Empty(PathAcl.UntrustedHolders(repaired, PathAcl.ContentRights | PathAcl.ControlRights, TrustedPrincipals.IsTrusted));
        Assert.Empty(Directory.GetFiles(audit));
        Assert.Single(Directory.GetFiles(journal));
    }

    [Fact]
    public void Repair_removes_a_link_planted_in_place_of_a_folder_without_touching_its_target()
    {
        Assert.SkipUnless(IsEnabled, "Integration test: set OTW_INTEGRATION=1 (elevated, lab machine) to run.");
        Directory.CreateDirectory(_root);
        string sentinel = Path.Combine(_elsewhere, "keep.txt");
        File.WriteAllText(sentinel, "target");
        string reports = Path.Combine(_root, "reports");
        Directory.CreateSymbolicLink(reports, _elsewhere);

        new WindowsSecureDirectory(_root, privileged: true, DistrustMe).EnsureSafeForWrite(reports);

        Assert.False(new DirectoryInfo(reports).Attributes.HasFlag(FileAttributes.ReparsePoint));
        Assert.True(File.Exists(sentinel));
        Assert.Empty(Directory.GetFiles(reports));
    }

    /// <summary>Writes a file and makes the (distrusted) current account its owner, as a planted file would be.</summary>
    private static void PlantOwnedByMe(string path, string content)
    {
        File.WriteAllText(path, content);
        var file = new FileInfo(path);
        FileSecurity security = file.GetAccessControl();
        security.SetOwner(Me);
        file.SetAccessControl(security);
    }

    private static void GrantUsersModify(string folder)
    {
        var info = new DirectoryInfo(folder);
        DirectorySecurity security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            TrustedPrincipals.Users, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(security);
    }
}
