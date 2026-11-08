using System.Text.Json;

namespace OpenTheWindows.Windows.Security;

/// <summary>One machine-policy value a run of this product wrote into the Local GPO.</summary>
/// <param name="RunId">The apply run that wrote it.</param>
/// <param name="Path">The key path under <c>HKEY_LOCAL_MACHINE</c>.</param>
/// <param name="Name">The value name.</param>
/// <param name="Value">The data written.</param>
public sealed record OwnedPolicyValue(Guid RunId, string Path, string Name, JsonElement Value);
