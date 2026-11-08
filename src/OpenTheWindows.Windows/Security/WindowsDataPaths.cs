namespace OpenTheWindows.Windows.Security;

/// <summary>
/// The product data folder (<c>%ProgramData%\OpenTheWindows</c>) and its
/// subfolders, with who may read each. Every subfolder is owned by Administrators
/// and writable only by Administrators and SYSTEM.
/// </summary>
public static class WindowsDataPaths
{
    /// <summary>The product data root.</summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenTheWindows");

    /// <summary>Journals (readable by Users so history can be listed unelevated; MAC-protected).</summary>
    public static string Journal { get; } = Path.Combine(Root, JournalFolderName);

    /// <summary>Monthly audit JSONL files.</summary>
    public static string Audit { get; } = Path.Combine(Root, "audit");

    /// <summary>Reports written by the scheduled remediation and audit tasks.</summary>
    public static string Reports { get; } = Path.Combine(Root, "reports");

    /// <summary>Public keys trusted to sign profiles and catalogues.</summary>
    public static string TrustedKeys { get; } = Path.Combine(Root, "trusted-keys");

    /// <summary>Profile files copied here for the scheduled task and the per-user logon fallback to read.</summary>
    public static string Profiles { get; } = Path.Combine(Root, "profiles");

    /// <summary>Product state kept between runs (the policy-ownership ledger, the persistence-watch baseline).</summary>
    public static string State { get; } = Path.Combine(Root, "state");

    /// <summary>The name of the folder holding machine secrets (the journal MAC key); administrators and SYSTEM only.</summary>
    public const string KeysFolderName = "keys";

    /// <summary>The name of the journal folder, whose files are never deleted by a repair (an untrusted journal is reported as tampered instead).</summary>
    public const string JournalFolderName = "journal";

    /// <summary>Machine secrets (the journal MAC key).</summary>
    public static string Keys { get; } = Path.Combine(Root, KeysFolderName);
}
