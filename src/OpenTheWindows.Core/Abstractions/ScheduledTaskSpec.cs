namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// A declarative, OS-agnostic description of a scheduled task to register. The
/// Core layer builds this (deterministically, so it can be asserted in tests); the
/// Windows layer translates it into a Task Scheduler registration.
/// </summary>
/// <param name="TaskPath">Full task path, e.g. <c>\OpenTheWindows\Remediate</c>.</param>
/// <param name="Description">Human-readable task description.</param>
/// <param name="Executable">Absolute path of the program to run.</param>
/// <param name="Arguments">The command-line arguments passed to <paramref name="Executable"/>.</param>
/// <param name="Trigger">When the task fires.</param>
/// <param name="RunAsSystem">Run as the built-in SYSTEM account (no stored credentials).</param>
/// <param name="HighestPrivileges">Run with the highest available privileges.</param>
/// <param name="Hidden">Hide the task from the default Task Scheduler view.</param>
public sealed record ScheduledTaskSpec(
    string TaskPath,
    string Description,
    string Executable,
    string Arguments,
    TaskTrigger Trigger,
    bool RunAsSystem,
    bool HighestPrivileges,
    bool Hidden);
