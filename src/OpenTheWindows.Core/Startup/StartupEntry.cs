namespace OpenTheWindows.Core.Startup;

/// <summary>A Run-key value or Startup-folder item as registered, before its startup switch is read.</summary>
/// <param name="Location">Where it is registered.</param>
/// <param name="Name">The Run value name, or the Startup-folder file name.</param>
/// <param name="Command">The command line (Run) or the file path (Startup folder).</param>
public sealed record StartupEntry(StartupLocation Location, string Name, string Command);
