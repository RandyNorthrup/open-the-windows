using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Services;

/// <summary>The persisted GUI preferences. Schema version is bumped when the shape changes.</summary>
/// <param name="SchemaVersion">The document version; currently 1.</param>
/// <param name="CreateRestorePoint">Whether apply creates a System Restore point first.</param>
/// <param name="RestartExplorerAutomatically">Whether apply restarts Explorer when a change needs it.</param>
/// <param name="EnterpriseMode">Whether consumer-only built-in profiles are hidden.</param>
/// <param name="Theme">The Fluent theme choice.</param>
internal sealed record GuiSettingsSnapshot(
    int SchemaVersion,
    bool CreateRestorePoint,
    bool RestartExplorerAutomatically,
    bool EnterpriseMode,
    AppTheme Theme)
{
    /// <summary>Current on-disk schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Factory defaults used when no file exists.</summary>
    public static GuiSettingsSnapshot Defaults { get; } = new(
        CurrentSchemaVersion,
        CreateRestorePoint: true,
        RestartExplorerAutomatically: false,
        EnterpriseMode: false,
        Theme: AppTheme.System);
}
