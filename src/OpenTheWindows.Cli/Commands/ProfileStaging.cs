using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// Copies a profile file, with its detached signature, into the protected profile
/// folder before a SYSTEM scheduled task or the per-user logon fallback is pointed at
/// it. Left where the operator had it (Downloads, a share, a relative path), the file
/// could be edited by a standard user, or resolve against the task's working folder,
/// between registration and every later run. Built-in ids and level names pass
/// through unchanged.
/// </summary>
internal static class ProfileStaging
{
    /// <summary>Returns the value a task or stub should pass to <c>--profile</c>.</summary>
    public static string Stage(string profileValue, CliHardening hardening)
    {
        ArgumentNullException.ThrowIfNull(profileValue);
        ArgumentNullException.ThrowIfNull(hardening);
        if (!File.Exists(profileValue))
        {
            return profileValue;
        }

        hardening.DataDirectories.EnsureSafeForWrite(hardening.ProfileStagingDirectory);
        string target = Path.Combine(hardening.ProfileStagingDirectory, Path.GetFileName(profileValue));
        File.Copy(Path.GetFullPath(profileValue), target, overwrite: true);

        string signature = profileValue + ProfileTrust.SignatureSuffix;
        string stagedSignature = target + ProfileTrust.SignatureSuffix;
        if (File.Exists(signature))
        {
            File.Copy(signature, stagedSignature, overwrite: true);
        }
        else if (File.Exists(stagedSignature))
        {
            // A stale signature from an earlier copy must not appear to cover this file.
            File.Delete(stagedSignature);
        }

        return target;
    }
}
