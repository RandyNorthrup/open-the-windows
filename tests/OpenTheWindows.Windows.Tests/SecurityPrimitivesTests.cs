using System.Security.AccessControl;
using System.Security.Principal;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The ACL rules behind the data-folder hardening and the executable-location
/// guard, evaluated over in-memory security descriptors (safe on any machine), plus
/// the guard and run lock against real, read-only or private OS objects.
/// </summary>
public sealed class SecurityPrimitivesTests
{
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier AuthenticatedUsers = new(WellKnownSidType.AuthenticatedUserSid, null);

    private static bool AdminsOnly(SecurityIdentifier sid) => sid == Administrators;

    private static DirectorySecurity Folder(SecurityIdentifier owner, params FileSystemAccessRule[] rules)
    {
        var security = new DirectorySecurity();
        security.SetOwner(owner);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (FileSystemAccessRule rule in rules)
        {
            security.AddAccessRule(rule);
        }

        return security;
    }

    private static FileSystemAccessRule Allow(SecurityIdentifier sid, FileSystemRights rights, PropagationFlags propagation = PropagationFlags.None)
        => new(sid, rights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, propagation, AccessControlType.Allow);

    [Fact]
    public void An_admin_owned_folder_that_users_can_only_read_is_safe()
    {
        DirectorySecurity security = Folder(Administrators,
            Allow(Administrators, FileSystemRights.FullControl), Allow(Users, FileSystemRights.ReadAndExecute));

        Assert.Empty(PathAcl.UntrustedHolders(security, PathAcl.ContentRights | PathAcl.ControlRights, AdminsOnly));
    }

    [Fact]
    public void A_folder_users_can_add_files_to_is_unsafe()
    {
        DirectorySecurity security = Folder(Administrators,
            Allow(Administrators, FileSystemRights.FullControl), Allow(Users, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories));

        Assert.Contains(Users, PathAcl.UntrustedHolders(security, PathAcl.ContentRights, AdminsOnly));
    }

    [Fact]
    public void A_folder_a_standard_user_owns_is_unsafe_whatever_its_entries_say()
    {
        DirectorySecurity security = Folder(Users, Allow(Administrators, FileSystemRights.FullControl));

        Assert.Contains(Users, PathAcl.UntrustedHolders(security, PathAcl.ContentRights, AdminsOnly));
    }

    [Fact]
    public void Inherit_only_and_deny_entries_do_not_grant_access_to_the_folder_itself()
    {
        DirectorySecurity security = Folder(Administrators,
            Allow(AuthenticatedUsers, FileSystemRights.Modify, PropagationFlags.InheritOnly),
            new FileSystemAccessRule(Users, FileSystemRights.Modify, AccessControlType.Deny));

        Assert.Empty(PathAcl.UntrustedHolders(security, PathAcl.ContentRights | PathAcl.ControlRights, AdminsOnly));
    }

    [Fact]
    public void Adding_a_subfolder_is_not_dangerous_on_an_ancestor_but_deleting_is()
    {
        // C:\ lets Authenticated Users create folders; that cannot replace an existing one.
        DirectorySecurity root = Folder(Administrators, Allow(AuthenticatedUsers, FileSystemRights.AppendData));
        DirectorySecurity deletable = Folder(Administrators, Allow(AuthenticatedUsers, FileSystemRights.Delete));

        Assert.Empty(PathAcl.UntrustedHolders(root, PathAcl.ControlRights, AdminsOnly));
        Assert.NotEmpty(PathAcl.UntrustedHolders(deletable, PathAcl.ControlRights, AdminsOnly));
    }

    [Fact]
    public void A_system_binary_is_a_safe_location_to_register()
        => Assert.Null(new WindowsExecutableLocationGuard().FindWeakness(Path.Combine(Environment.SystemDirectory, "cmd.exe")));

    [Fact]
    public void A_missing_executable_is_refused()
        => Assert.NotNull(new WindowsExecutableLocationGuard().FindWeakness(Path.Combine(Path.GetTempPath(), "otw-missing-" + Guid.NewGuid().ToString("N") + ".exe")));

    [Fact]
    public void A_folder_outside_the_data_root_is_simply_created()
    {
        string root = Directory.CreateTempSubdirectory("otw-data-root").FullName;
        string outside = Path.Combine(Path.GetTempPath(), "otw-outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            new WindowsSecureDirectory(root, privileged: false, TrustedPrincipals.IsTrusted).EnsureSafeForWrite(outside);

            Assert.True(Directory.Exists(outside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void The_run_lock_excludes_a_second_holder_until_released()
    {
        // A mutex is re-entrant for its owning thread and may only be released by it, so each
        // holder runs on its own dedicated thread.
        var runLock = new WindowsRunLock(@"Local\OpenTheWindows.Test." + Guid.NewGuid().ToString("N"));
        using var holding = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool firstAcquired = false;
        var holder = new Thread(() =>
        {
            using IDisposable? held = runLock.TryAcquire(TimeSpan.Zero);
            firstAcquired = held is not null;
            holding.Set();
            release.Wait();
        });
        holder.Start();
        holding.Wait(TestContext.Current.CancellationToken);

        bool contenderAcquired = TryOnNewThread(runLock);
        release.Set();
        holder.Join();
        bool reacquired = TryOnNewThread(runLock);

        Assert.True(firstAcquired);
        Assert.False(contenderAcquired);
        Assert.True(reacquired);
    }

    private static bool TryOnNewThread(WindowsRunLock runLock)
    {
        bool acquired = false;
        var thread = new Thread(() =>
        {
            using IDisposable? held = runLock.TryAcquire(TimeSpan.Zero);
            acquired = held is not null;
        });
        thread.Start();
        thread.Join();
        return acquired;
    }
}
