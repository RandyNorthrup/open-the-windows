namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads a Windows service's configuration and run state. Never writes.</summary>
public interface IServiceReader
{
    /// <summary>Reads the service, or returns <see langword="null"/> when it does not exist.</summary>
    ServiceSnapshot? Read(string name);
}
