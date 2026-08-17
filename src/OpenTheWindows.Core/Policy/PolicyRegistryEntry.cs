namespace OpenTheWindows.Core.Policy;

/// <summary>
/// One value entry parsed from a Group Policy registry file
/// (<c>Registry.pol</c>, the binary PReg format).
/// </summary>
/// <param name="Key">
/// Registry key path relative to the hive root, with no <c>HKLM</c>/<c>HKCU</c>
/// prefix (the file's location determines the root).
/// </param>
/// <param name="ValueName">
/// The value name. May be a special directive such as <c>**Del.&lt;name&gt;</c>
/// (managed removal of a value) or <c>**DelVals</c> (remove all values in the key).
/// </param>
/// <param name="Type">Registry value type (<c>REG_SZ</c> = 1, <c>REG_DWORD</c> = 4, …).</param>
/// <param name="Data">The raw value bytes exactly as stored in the file.</param>
public sealed record PolicyRegistryEntry(string Key, string ValueName, uint Type, ReadOnlyMemory<byte> Data);
