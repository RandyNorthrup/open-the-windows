using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Tests.Journal;

/// <summary>
/// Round-trip, hashing and null-omission tests for the journal serializer — the
/// tamper-evidence core that the store and the apply engine both rely on.
/// </summary>
public sealed class JournalSerializerTests
{
    private static JournalRecord Record()
    {
        var action = new RegistryAction(
            RegistryHive.LocalMachine, @"SOFTWARE\Test", "Value",
            RegistryValueType.Dword, JsonSerializer.SerializeToElement(1), JsonSerializer.SerializeToElement(0), RegistryValueKind.Preference);

        return new JournalRecord
        {
            RunId = new Guid("11111111-1111-1111-1111-111111111111"),
            StartedAt = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
            AppVersion = "0.2.0",
            Os = new JournalOs(26100, 4351, "Professional"),
            Profile = "balanced",
            Operator = @"TEST\tester",
            RestorePoint = new JournalRestorePoint(false, false, "NotRequested", null),
            Entries =
            [
                new JournalEntry
                {
                    Id = "privacy.test.entry",
                    Risk = RiskTier.Safe,
                    Requires = RestartRequirement.None,
                    Result = ApplyState.Applied,
                    Actions =
                    [
                        new JournalAction
                        {
                            Index = 0,
                            Kind = "Registry",
                            Target = @"HKLM\SOFTWARE\Test!Value",
                            Action = action,
                            Prior = new JournalActionState { Exists = true, Type = RegistryValueType.Dword, Data = JsonSerializer.SerializeToElement(0) },
                            Desired = new JournalActionState { Type = RegistryValueType.Dword, Data = JsonSerializer.SerializeToElement(1) },
                            Result = ApplyState.Applied,
                            Verified = true,
                        },
                    ],
                },
            ],
            Result = RunState.Completed,
        };
    }

    [Fact]
    public void Round_trips_a_record()
    {
        JournalRecord original = Record();
        original.Hash = JournalSerializer.ComputeHash(original);

        JournalRecord parsed = JournalSerializer.Deserialize(JournalSerializer.Serialize(original));

        Assert.Equal(original.RunId, parsed.RunId);
        Assert.Equal(original.Profile, parsed.Profile);
        Assert.Single(parsed.Entries);
        Assert.Equal("Registry", parsed.Entries[0].Actions[0].Kind);
        Assert.IsType<RegistryAction>(parsed.Entries[0].Actions[0].Action);
        Assert.True(JournalSerializer.VerifyHash(parsed));
    }

    [Fact]
    public void Hash_changes_when_content_changes()
    {
        JournalRecord record = Record();
        string before = JournalSerializer.ComputeHash(record);

        record.Result = RunState.RolledBack;
        string after = JournalSerializer.ComputeHash(record);

        Assert.NotEqual(before, after, StringComparer.Ordinal);
    }

    [Fact]
    public void Verify_fails_when_a_record_is_tampered()
    {
        JournalRecord record = Record();
        record.Hash = JournalSerializer.ComputeHash(record);

        record.Entries[0].Result = ApplyState.Failed; // tamper after hashing

        Assert.False(JournalSerializer.VerifyHash(record));
    }

    [Fact]
    public void Null_union_fields_are_omitted_from_the_action_state()
    {
        JournalRecord record = Record();
        record.Hash = JournalSerializer.ComputeHash(record);

        string json = JournalSerializer.Serialize(record);

        Assert.True(json.Contains("\"exists\": true", StringComparison.Ordinal));
        Assert.False(json.Contains("startType", StringComparison.Ordinal));
        Assert.False(json.Contains("\"ac\"", StringComparison.Ordinal));
    }
}
