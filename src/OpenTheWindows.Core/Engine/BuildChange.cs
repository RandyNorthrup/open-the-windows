using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>Compares the live OS build with the build of the most recent journal.</summary>
public static class BuildChange
{
    /// <summary>
    /// Detects a build change between the live OS facts and the OS recorded in the
    /// most recent journal. With no <paramref name="previous"/> record (a first run
    /// on this machine) the result is <c>Changed = true</c> with a <see langword="null"/>
    /// <c>Previous</c>: there is no baseline, so the safe, reversible action is to apply.
    /// </summary>
    public static BuildChangeStatus Detect(OperatingSystemFacts current, JournalOs? previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null)
        {
            return new BuildChangeStatus(true, current.FullBuild, null);
        }

        bool changed = current.Build != previous.Build || current.Revision != previous.Revision;
        return new BuildChangeStatus(changed, current.FullBuild, $"{previous.Build}.{previous.Revision}");
    }
}
