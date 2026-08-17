namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>Sets a power setting on the active scheme (AC and DC values).</summary>
/// <param name="SubgroupGuid">Power subgroup GUID.</param>
/// <param name="SettingGuid">Power setting GUID.</param>
/// <param name="AcValue">Value on AC power.</param>
/// <param name="DcValue">Value on battery.</param>
public sealed record PowerSettingAction(Guid SubgroupGuid, Guid SettingGuid, uint AcValue, uint DcValue) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "PowerSetting";
}
