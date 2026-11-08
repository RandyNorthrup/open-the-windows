using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace OpenTheWindows.Windows.Interop;

/// <summary>
/// Reads the direct members of a local group (for example BUILTIN\Administrators)
/// through <c>NetLocalGroupGetMembers</c>, so a file or ACE held by an individual
/// administrator account can be recognised as administrator-controlled.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class LocalGroups
{
    private const int MembersInfoLevelSid = 0;
    private const int MaxPreferredLength = -1;
    private const int Success = 0;

    /// <summary>Returns the SIDs of the direct members of the local group <paramref name="group"/>.</summary>
    /// <exception cref="Win32Exception">The group could not be enumerated.</exception>
    public static IReadOnlyList<SecurityIdentifier> DirectMembers(SecurityIdentifier group)
    {
        ArgumentNullException.ThrowIfNull(group);
        string account = ((NTAccount)group.Translate(typeof(NTAccount))).Value;
        int separator = account.IndexOf('\\', StringComparison.Ordinal);
        string groupName = separator >= 0 ? account[(separator + 1)..] : account;

        nint resume = nint.Zero;
        int status = NetLocalGroupGetMembers(null, groupName, MembersInfoLevelSid, out nint buffer, MaxPreferredLength, out uint read, out _, ref resume);
        if (status != Success)
        {
            throw new Win32Exception(status, "NetLocalGroupGetMembers failed.");
        }

        try
        {
            List<SecurityIdentifier> members = [];
            for (int i = 0; i < read; i++)
            {
                nint sid = Marshal.ReadIntPtr(buffer, i * nint.Size);
                members.Add(new SecurityIdentifier(sid));
            }

            return members;
        }
        finally
        {
            _ = NetApiBufferFree(buffer);
        }
    }

    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int NetLocalGroupGetMembers(
        string? serverName, string localGroupName, int level, out nint buffer, int preferredMaximumLength,
        out uint entriesRead, out uint totalEntries, ref nint resumeHandle);

    [LibraryImport("netapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int NetApiBufferFree(nint buffer);
}
