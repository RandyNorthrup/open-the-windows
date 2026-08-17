namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads Appx/MSIX package installation state. Never writes.</summary>
public interface IAppxReader
{
    /// <summary>Reads the package's state for the given user (and machine-wide).</summary>
    /// <param name="packageFamilyName">Package family name (Name_PublisherId).</param>
    /// <param name="userSid">Target user SID, or <see langword="null"/> for the current context.</param>
    AppxSnapshot Read(string packageFamilyName, string? userSid);
}
