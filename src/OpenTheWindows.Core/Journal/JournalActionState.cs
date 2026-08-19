using System.Text.Json;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Journal;

/// <summary>
/// A kind-specific before/after state captured for one action. Only the fields
/// relevant to the action's kind are populated; the rest are <see langword="null"/>
/// and omitted from the JSON. This is what revert replays to restore the prior
/// state exactly, including "the value was absent".
/// </summary>
public sealed record JournalActionState
{
    /// <summary>Registry/task/feature/appx: whether the target existed before the change.</summary>
    public bool? Exists { get; init; }

    /// <summary>Registry: the value type.</summary>
    public RegistryValueType? Type { get; init; }

    /// <summary>Registry: the value data as JSON; also the Defender preference value.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>Service: the configured start type.</summary>
    public ServiceStartType? StartType { get; init; }

    /// <summary>Service: whether it was running.</summary>
    public bool? Running { get; init; }

    /// <summary>Scheduled task / optional feature: whether it was enabled.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Appx: installed for the target user.</summary>
    public bool? InstalledForUser { get; init; }

    /// <summary>Appx: installed for any user.</summary>
    public bool? InstalledForAnyUser { get; init; }

    /// <summary>Appx: deprovisioned (removed for future users).</summary>
    public bool? Deprovisioned { get; init; }

    /// <summary>Power: the AC value index.</summary>
    public uint? Ac { get; init; }

    /// <summary>Power: the DC value index.</summary>
    public uint? Dc { get; init; }

    /// <summary>Security policy: the setting's value (from the <c>secedit</c> export).</summary>
    public string? SecurityValue { get; init; }
}
