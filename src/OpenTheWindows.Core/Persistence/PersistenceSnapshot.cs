namespace OpenTheWindows.Core.Persistence;

/// <summary>Every autostart found on the machine at one moment, kept as the baseline later states are compared with.</summary>
/// <param name="SchemaVersion">The snapshot format version (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="TakenAt">When the snapshot was taken.</param>
/// <param name="Items">The autostarts found.</param>
public sealed record PersistenceSnapshot(int SchemaVersion, DateTimeOffset TakenAt, IReadOnlyList<PersistenceItem> Items)
{
    /// <summary>The snapshot format this build writes and reads.</summary>
    public const int CurrentSchemaVersion = 1;
}
