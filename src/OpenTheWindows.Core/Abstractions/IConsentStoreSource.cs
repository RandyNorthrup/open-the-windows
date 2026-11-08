using OpenTheWindows.Core.Privacy;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads the CapabilityAccessManager ConsentStore usage records (read-only).</summary>
public interface IConsentStoreSource
{
    /// <summary>The machine-wide records and, when <paramref name="userSid"/> is given, that user's.</summary>
    IReadOnlyList<ConsentStoreRecord> List(string? userSid);
}
