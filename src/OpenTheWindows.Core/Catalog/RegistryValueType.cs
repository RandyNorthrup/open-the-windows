namespace OpenTheWindows.Core.Catalog;

/// <summary>Registry value types the catalogue may write.</summary>
public enum RegistryValueType
{
    /// <summary>REG_DWORD; JSON number 0..4294967295.</summary>
    Dword = 0,

    /// <summary>REG_QWORD; JSON number.</summary>
    Qword = 1,

    /// <summary>REG_SZ; JSON string.</summary>
    Sz = 2,

    /// <summary>REG_EXPAND_SZ; JSON string.</summary>
    ExpandSz = 3,

    /// <summary>REG_MULTI_SZ; JSON array of strings.</summary>
    MultiSz = 4,

    /// <summary>REG_BINARY; JSON base64 string.</summary>
    Binary = 5,
}
