using System.Text.Json;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// The observed state of a single registry value. <see cref="Data"/> is the raw
/// value as JSON (number, string, string array or base64) so it can be compared
/// against a catalogue action's declared value without a type-specific reader.
/// </summary>
/// <param name="Exists">Whether the value is present.</param>
/// <param name="Type">The value's registry type, or <see langword="null"/> when absent.</param>
/// <param name="Data">The value's data as JSON, or <see langword="null"/> when absent.</param>
public sealed record RegistryValueSnapshot(bool Exists, RegistryValueType? Type, JsonElement? Data);
