using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Restore-point stub returning a configurable result and counting calls.</summary>
public sealed class FakeRestorePointService : IRestorePointService
{
    /// <summary>The result <see cref="Create"/> returns.</summary>
    public RestorePointResult Result { get; set; } = new(RestorePointStatus.Created, 42, "Restore point created.");

    /// <summary>How many times a restore point was requested.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public RestorePointResult Create(string description)
    {
        Calls++;
        return Result;
    }
}
