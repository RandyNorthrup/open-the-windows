using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Which machines a catalogue entry applies to. Empty lists mean "any".
/// </summary>
/// <param name="Editions">Edition families the entry is honoured on. Empty = all editions.</param>
/// <param name="MinBuild">Lowest OS build (inclusive) the entry applies to; null = no lower bound beyond Windows 11.</param>
/// <param name="MaxBuild">Highest OS build (inclusive); null = no upper bound.</param>
/// <param name="Architectures">"x64", "arm64"; empty = all.</param>
public sealed record Applicability(
    IReadOnlyList<WindowsEdition> Editions,
    int? MinBuild,
    int? MaxBuild,
    IReadOnlyList<string> Architectures)
{
    /// <summary>Applies everywhere Windows 11 runs.</summary>
    public static Applicability Any { get; } = new([], null, null, []);

    /// <summary>
    /// Returns <see langword="true"/> when the entry applies to the given machine.
    /// An unknown edition id never matches an entry that restricts editions.
    /// </summary>
    public bool Matches(OperatingSystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (MinBuild is int min && facts.Build < min)
        {
            return false;
        }

        if (MaxBuild is int max && facts.Build > max)
        {
            return false;
        }

        if (Architectures.Count > 0
            && !Architectures.Contains(facts.Architecture, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Editions.Count > 0)
        {
            WindowsEdition? edition = WindowsEditions.FromEditionId(facts.EditionId);
            if (edition is null || !Editions.Contains(edition.Value))
            {
                return false;
            }
        }

        return true;
    }
}
