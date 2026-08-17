using System.CommandLine;

namespace OpenTheWindows.Cli.Commands;

/// <summary>The options of <see cref="ApplyCommand"/>, grouped so the action stays small.</summary>
internal sealed class ApplyCommandOptions
{
    public CommonProfileOptions Common { get; } = new();

    public Option<bool> WhatIf { get; } = new("--what-if")
    {
        Description = "Plan only; make no changes.",
    };

    public Option<bool> NoRestorePoint { get; } = new("--no-restore-point")
    {
        Description = "Do not create a System Restore point first (one is created by default).",
    };

    public Option<bool> BreakGlass { get; } = new("--break-glass")
    {
        Description = "Apply even over MDM/Group-Policy-managed settings (audited).",
    };

    public Option<bool> AllowAdvanced { get; } = new("--allow-advanced")
    {
        Description = "Permit entries with Advanced risk.",
    };

    public Option<bool> AllowBreaking { get; } = new("--allow-breaking")
    {
        Description = "Permit entries with Breaking risk (also needs typed confirmation unless --yes).",
    };

    public Option<bool> Yes { get; } = new("--yes")
    {
        Description = "Assume yes to confirmations (unattended).",
    };

    public Option<bool> RestartExplorer { get; } = new("--restart-explorer")
    {
        Description = "Restart Explorer for entries that require it.",
    };

    public Option<bool> Json { get; } = new("--json")
    {
        Description = "Emit the result as JSON on stdout (stable schema).",
    };

    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Common.AddTo(command);
        command.Options.Add(WhatIf);
        command.Options.Add(NoRestorePoint);
        command.Options.Add(BreakGlass);
        command.Options.Add(AllowAdvanced);
        command.Options.Add(AllowBreaking);
        command.Options.Add(Yes);
        command.Options.Add(RestartExplorer);
        command.Options.Add(Json);
    }
}
