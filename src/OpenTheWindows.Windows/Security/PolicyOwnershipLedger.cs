using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// Records which machine-policy values this product itself wrote into the Local GPO,
/// so the managed-setting detector can tell its own policy values from an
/// administrator's or an organisation's. Without it, every value the product had set
/// through the Local GPO would read as "managed by Group Policy" on the next run, and
/// changing profile or remediating drift would be refused. A value counts as owned
/// while the live value equals what a not-yet-reverted run wrote.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PolicyOwnershipLedger
{
    private const string FileName = "owned-policies.json";
    private const string TemporarySuffix = ".tmp";

    private readonly string _directory;
    private readonly ISecureDirectory? _guard;
    private readonly Lock _gate = new();

    /// <summary>The machine ledger under <c>%ProgramData%\OpenTheWindows\state</c>.</summary>
    public PolicyOwnershipLedger()
        : this(Path.Combine(WindowsDataPaths.Root, "state"), new WindowsSecureDirectory())
    {
    }

    /// <summary>A ledger in <paramref name="directory"/> (tests: <paramref name="guard"/> may be null).</summary>
    internal PolicyOwnershipLedger(string directory, ISecureDirectory? guard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _guard = guard;
    }

    private string LedgerPath => Path.Combine(_directory, FileName);

    /// <summary>Whether this product wrote <paramref name="liveValue"/> to <paramref name="path"/>\<paramref name="name"/> and still owns it.</summary>
    public bool IsOwned(string path, string name, JsonElement? liveValue)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(name);
        if (liveValue is not { } live)
        {
            return false;
        }

        return Read().Exists(v => Matches(v, path, name) && StateComparison.JsonEquals(v.Value, live));
    }

    /// <summary>
    /// Folds a finished run into the ledger: an apply adds the machine-policy values it
    /// wrote; a completed revert drops what the reverted run had added. Runs still in
    /// progress change nothing.
    /// </summary>
    public void Record(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Result == RunState.InProgress)
        {
            return;
        }

        lock (_gate)
        {
            List<OwnedPolicyValue> owned = Read();
            if (record.RevertOf is { } reverted)
            {
                if (record.Result != RunState.Completed)
                {
                    return;
                }

                owned.RemoveAll(v => v.RunId == reverted);
            }
            else
            {
                owned.RemoveAll(v => v.RunId == record.RunId);
                owned.AddRange(WrittenPolicies(record));
            }

            Write(owned);
        }
    }

    private static IEnumerable<OwnedPolicyValue> WrittenPolicies(JournalRecord record)
        => record.Entries
            .SelectMany(e => e.Actions)
            .Where(a => a.Result == ApplyState.Applied)
            .Select(a => a.Action)
            .OfType<RegistryAction>()
            .Where(r => r.Hive == RegistryHive.LocalMachine && r.ValueKind == RegistryValueKind.Policy)
            .Select(r => new OwnedPolicyValue(record.RunId, r.Path, r.Name, r.Value.Clone()));

    private static bool Matches(OwnedPolicyValue value, string path, string name)
        => string.Equals(value.Path, path, StringComparison.OrdinalIgnoreCase) && string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase);

    private List<OwnedPolicyValue> Read()
    {
        try
        {
            return File.Exists(LedgerPath)
                ? JsonSerializer.Deserialize(File.ReadAllText(LedgerPath, Encoding.UTF8), OwnershipJsonContext.Default.ListOwnedPolicyValue) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable ledger owns nothing: values fall back to "managed", the safe direction.
            return [];
        }
    }

    private void Write(List<OwnedPolicyValue> owned)
    {
        if (_guard is null)
        {
            Directory.CreateDirectory(_directory);
        }
        else
        {
            _guard.EnsureSafeForWrite(_directory);
        }

        string temporary = LedgerPath + TemporarySuffix;
        File.WriteAllText(temporary, JsonSerializer.Serialize(owned, OwnershipJsonContext.Default.ListOwnedPolicyValue), Encoding.UTF8);
        File.Move(temporary, LedgerPath, overwrite: true);
    }
}
