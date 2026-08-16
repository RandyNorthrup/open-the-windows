namespace OpenTheWindows.Core.Diagnostics;

/// <summary>Outcome of the pre-flight support check.</summary>
public enum SupportStatus
{
    /// <summary>Windows 11 client build; all features available.</summary>
    Supported = 0,

    /// <summary>Windows 10 or older client. The product refuses to apply changes.</summary>
    UnsupportedWindowsVersion = 1,

    /// <summary>Windows Server. The product refuses to apply changes.</summary>
    UnsupportedServerSku = 2,
}
