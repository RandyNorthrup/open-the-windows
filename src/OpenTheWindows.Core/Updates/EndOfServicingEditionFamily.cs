using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The two Windows 11 servicing tracks: consumer SKUs (Home, Pro, Pro Education,
/// Pro for Workstations) get 24 months of servicing per release; the enterprise
/// SKUs (Enterprise, Education, IoT Enterprise) get 36 (research 03 §4).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EndOfServicingEditionFamily>))]
public enum EndOfServicingEditionFamily
{
    /// <summary>Home, Pro, Pro Education, Pro for Workstations.</summary>
    Consumer = 0,

    /// <summary>Enterprise, Education, IoT Enterprise, Enterprise multi-session.</summary>
    Enterprise = 1,
}
