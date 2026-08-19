using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// The severity / profile label a baseline rule carries. The JSON form matches
/// each framework's own vocabulary (DISA STIG category, CIS profile level,
/// Microsoft baseline). Scoring weights each level in <c>AuditScoring</c>.
/// </summary>
public enum BaselineSeverity
{
    /// <summary>DISA STIG Category I (high).</summary>
    [JsonStringEnumMemberName("CAT I")]
    CatI,

    /// <summary>DISA STIG Category II (medium).</summary>
    [JsonStringEnumMemberName("CAT II")]
    CatII,

    /// <summary>DISA STIG Category III (low).</summary>
    [JsonStringEnumMemberName("CAT III")]
    CatIII,

    /// <summary>CIS Level 1 (baseline, minimal operational impact).</summary>
    [JsonStringEnumMemberName("L1")]
    L1,

    /// <summary>CIS Level 2 (defence-in-depth, may reduce usability).</summary>
    [JsonStringEnumMemberName("L2")]
    L2,

    /// <summary>CIS BitLocker (BL) section.</summary>
    [JsonStringEnumMemberName("BL")]
    Bl,

    /// <summary>CIS Next Generation Windows Security (NG) section.</summary>
    [JsonStringEnumMemberName("NG")]
    Ng,

    /// <summary>A Microsoft Security Baseline setting.</summary>
    [JsonStringEnumMemberName("Baseline")]
    Baseline,
}
