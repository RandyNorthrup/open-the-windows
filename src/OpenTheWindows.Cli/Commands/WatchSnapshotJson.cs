namespace OpenTheWindows.Cli.Commands;

/// <summary>The <c>watch snapshot --json</c> result.</summary>
/// <param name="TakenAt">When the baseline was taken.</param>
/// <param name="Count">How many autostarts it records.</param>
internal sealed record WatchSnapshotJson(DateTimeOffset TakenAt, int Count);
