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
        return Convert.ToHexStringLower(SHA256.HashData(UnhashedBytes(record)));
    }

    /// <summary>
    /// Computes the HMAC-SHA256 (lowercase hex) of the record with its own
    /// <c>hash</c> field omitted, under the machine journal <paramref name="key"/>.
    /// Unlike <see cref="ComputeHash"/>, nobody without the key can produce a value
    /// that verifies, so a forged journal is detected.
    /// </summary>
    public static string ComputeMac(JournalRecord record, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, UnhashedBytes(record)));
    }

    /// <summary>Recomputes <paramref name="record"/>'s plain hash and returns whether it matches the stored <c>Hash</c>.</summary>
    public static bool VerifyHash(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Hash is { } stored && FixedTimeEquals(stored, ComputeHash(record));
    }

    /// <summary>
    /// Verifies <paramref name="record"/> under <paramref name="key"/>: a keyed record
    /// must carry a matching MAC, an unkeyed one a matching plain hash. A keyed record
    /// never verifies without the key.
    /// </summary>
    public static bool Verify(JournalRecord record, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Keyed != true)
        {
            return VerifyHash(record);
        }

        return !key.IsEmpty && record.Hash is { } stored && FixedTimeEquals(stored, ComputeMac(record, key));
    }

    private static byte[] UnhashedBytes(JournalRecord record)
    {
        string? saved = record.Hash;
        try
        {
            record.Hash = null;
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record, JournalJsonContext.Default.JournalRecord));
        }
        finally
        {
            record.Hash = saved;
        }
    }

    private static bool FixedTimeEquals(string left, string right)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
}
