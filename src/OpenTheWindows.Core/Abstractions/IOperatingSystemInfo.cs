namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Read-only facts about the operating system the engine is running on.
/// Implemented in <c>OpenTheWindows.Windows</c>; faked in tests.
/// </summary>
public interface IOperatingSystemInfo
{
    /// <summary>Returns the facts for the current machine.</summary>
    OperatingSystemFacts GetFacts();
}
