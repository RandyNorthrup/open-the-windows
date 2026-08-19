namespace OpenTheWindows.App.ViewModels;

/// <summary>The signing state of a profile shown on the Profiles page.</summary>
internal enum ProfileSignatureStatus
{
    /// <summary>A profile that ships inside the product; not subject to signing.</summary>
    BuiltIn,

    /// <summary>An imported file with no detached signature.</summary>
    Unsigned,

    /// <summary>An imported file signed by a key that is not in the machine trust store.</summary>
    Untrusted,

    /// <summary>An imported file signed by a trusted key.</summary>
    Trusted,

    /// <summary>An imported file whose signature file is present but unreadable/invalid.</summary>
    Invalid,
}
