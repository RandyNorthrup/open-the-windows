namespace OpenTheWindows.Core.Persistence;

/// <summary>One thing that makes code run without the user starting it: a service, a scheduled task, a Run value or a Startup-folder item.</summary>
/// <param name="Kind">What kind of autostart it is.</param>
/// <param name="Key">Its identity within the kind (service name, task path, Run key and value name, or Startup-folder path).</param>
/// <param name="Detail">What it runs and how (command line, start type, enabled state); a change here is a change to the item.</param>
public sealed record PersistenceItem(PersistenceKind Kind, string Key, string Detail);
