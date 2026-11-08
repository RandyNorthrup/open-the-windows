using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The unprivileged side of the data-folder hardening, over temporary folders the test
/// process owns. "Trusted" is injected: either the real administrator set plus the
/// current account (the folder is then as safe as a hardened one) or only the real
/// administrator set minus the current account (the folder is then as unsafe as one a
/// standard user created). The privileged repair itself runs on the lab machine.
/// </summary>
public sealed class SecureDataFolderTests : IDisposable
{
    private static readonly SecurityIdentifier Me = WindowsIdentity.GetCurrent().User!;

    private readonly string _root = PrivateFolder();

    private static bool TrustMe(SecurityIdentifier sid) => sid == Me || (sid != Me && TrustedPrincipals.IsTrusted(sid));

    private static bool DistrustMe(SecurityIdentifier sid) => sid != Me && TrustedPrincipals.IsTrusted(sid);

    private WindowsSecureDirectory Directories(Func<SecurityIdentifier, bool> isTrusted) => new(_root, privileged: false, isTrusted);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// A temporary folder whose permissions the test controls (SYSTEM, Administrators and
    /// the current account only), independent of whatever the machine's Temp folder grants.
    /// </summary>
    private static string PrivateFolder()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("otw-secure-root");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (SecurityIdentifier sid in new[] { TrustedPrincipals.LocalSystem, TrustedPrincipals.Administrators, Me })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        }

        folder.SetAccessControl(security);
        return folder.FullName;
    }

    [Fact]
    public void A_safe_existing_folder_is_accepted_for_writing_without_privilege()
    {
        string journal = Directory.CreateDirectory(Path.Combine(_root, "journal")).FullName;

        Directories(TrustMe).EnsureSafeForWrite(journal);

        Assert.True(Directories(TrustMe).IsTrustworthy(journal));
    }

    [Fact]
    public void A_folder_owned_by_an_untrusted_account_is_refused_without_privilege()
    {
        string journal = Directory.CreateDirectory(Path.Combine(_root, "journal")).FullName;

        Assert.Throws<UnauthorizedAccessException>(() => Directories(DistrustMe).EnsureSafeForWrite(journal));
        Assert.False(Directories(DistrustMe).IsTrustworthy(journal));
    }

    [Fact]
    public void A_missing_folder_holds_nothing_to_distrust()
    {
        string missingRoot = Path.Combine(_root, "absent-root");

        Assert.True(new WindowsSecureDirectory(missingRoot, privileged: false, DistrustMe).IsTrustworthy(Path.Combine(missingRoot, "journal")));
        Assert.True(Directories(TrustMe).IsTrustworthy(Path.Combine(_root, "absent")));
    }

    [Fact]
    public void A_file_is_trusted_and_private_only_when_its_owner_is()
    {
        string file = Path.Combine(_root, "x.pem");
        File.WriteAllText(file, "x");

        Assert.True(Directories(TrustMe).IsTrustedFile(file));
        Assert.True(Directories(TrustMe).IsPrivateFile(file));
        Assert.False(Directories(DistrustMe).IsTrustedFile(file));
        Assert.False(Directories(TrustMe).IsTrustedFile(Path.Combine(_root, "missing.pem")));
    }

    [Fact]
    public void The_journal_key_is_created_once_and_read_back_by_a_later_process()
    {
        string keys = Path.Combine(_root, WindowsDataPaths.KeysFolderName);
        Directory.CreateDirectory(keys);

        byte[]? created = new MachineJournalProtection(Directories(TrustMe), keys).Key(create: true);
        byte[]? read = new MachineJournalProtection(Directories(TrustMe), keys).Key(create: false);

        Assert.NotNull(created);
        Assert.Equal(32, created.Length);
        Assert.Equal(created, read);
    }

    [Fact]
    public void A_key_file_an_untrusted_account_owns_is_never_used()
    {
        string keys = Directory.CreateDirectory(Path.Combine(_root, WindowsDataPaths.KeysFolderName)).FullName;
        File.WriteAllBytes(Path.Combine(keys, "journal.key"), RandomNumberGenerator.GetBytes(32));

        Assert.Null(new MachineJournalProtection(Directories(DistrustMe), keys).Key(create: false));
    }

    [Fact]
    public void Trusted_keys_are_read_only_from_a_trusted_folder()
    {
        string store = Directory.CreateDirectory(Path.Combine(_root, "trusted-keys")).FullName;
        File.WriteAllText(Path.Combine(store, "signer.pem"), "-----BEGIN PUBLIC KEY-----");

        Assert.Single(new WindowsTrustedKeySource(store, Directories(TrustMe)).ReadPublicKeysPem());
        Assert.Empty(new WindowsTrustedKeySource(store, Directories(DistrustMe)).ReadPublicKeysPem());
    }

    [Fact]
    public void An_executable_under_a_folder_a_standard_account_controls_is_flagged()
    {
        string exe = Path.Combine(_root, "otw.exe");
        File.WriteAllText(exe, "MZ");

        Assert.Contains("non-administrators", new WindowsExecutableLocationGuard(DistrustMe).FindWeakness(exe), StringComparison.Ordinal);
    }
}
