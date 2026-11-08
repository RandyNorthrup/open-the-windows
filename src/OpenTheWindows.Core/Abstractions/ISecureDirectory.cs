namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Prepares a directory that a privileged process (an elevated administrator or the
/// SYSTEM scheduled task) is about to write into. For the product's own data folder
/// the directory must be owned by and writable only by administrators and SYSTEM and
/// must not be a link, so a standard user can neither plant files in it nor redirect
/// a privileged write elsewhere; any other location (an operator's <c>--out</c>
/// path) is simply created.
/// </summary>
public interface ISecureDirectory
{
    /// <summary>
    /// Ensures <paramref name="directory"/> exists and is safe for a privileged write,
    /// repairing the product data folder's ownership and permissions when this process
    /// is privileged.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The directory is not safe and could not be made safe.</exception>
    void EnsureSafeForWrite(string directory);
}
