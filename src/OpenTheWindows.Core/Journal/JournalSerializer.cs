using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenTheWindows.Core.Journal;

/// <summary>
/// Serialises journal records and computes the tamper-evidence hash. Kept
/// Windows-free so the hash chain is verified the same way in unit tests
/// (in-memory store) and in production (<c>WindowsJournalStore</c>).
/// </summary>
public static class JournalSerializer
{
    /// <summary>Serialises a record to its on-disk JSON form (including its current <c>Hash</c>).</summary>
    public static string Serialize(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonSerializer.Serialize(record, JournalJsonContext.Default.JournalRecord);
    }

    /// <summary>Deserialises a record from its on-disk JSON form.</summary>
    public static JournalRecord Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, JournalJsonContext.Default.JournalRecord)
            ?? throw new JsonException("Journal document deserialised to null.");
    }

    /// <summary>
    /// Computes the SHA-256 (lowercase hex) of the record with its own
    /// <c>hash</c> field omitted, so a stored hash can be recomputed and
    /// compared without a chicken-and-egg dependency on itself.
    /// </summary>
    public static string ComputeHash(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string? saved = record.Hash;
        try
        {
            record.Hash = null;
            string json = JsonSerializer.Serialize(record, JournalJsonContext.Default.JournalRecord);
            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexStringLower(digest);
        }
        finally
        {
            record.Hash = saved;
        }
    }

    /// <summary>Recomputes <paramref name="record"/>'s hash and returns whether it matches the stored <c>Hash</c>.</summary>
    public static bool VerifyHash(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Hash is { } stored && string.Equals(stored, ComputeHash(record), StringComparison.Ordinal);
    }
}
