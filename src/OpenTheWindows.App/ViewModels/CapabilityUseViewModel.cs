using System.Globalization;
using OpenTheWindows.Core.Privacy;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One app's recent use of a capability, as shown on the App access page.</summary>
internal sealed class CapabilityUseViewModel
{
    /// <summary>Projects a use record.</summary>
    public CapabilityUseViewModel(CapabilityUse use)
    {
        ArgumentNullException.ThrowIfNull(use);
        CapabilityLabel = use.Capability switch
        {
            CapabilityAccessService.Webcam => "Camera",
            CapabilityAccessService.Microphone => "Microphone",
            CapabilityAccessService.Location => "Location",
            _ => use.Capability,
        };
        App = use.App;
        InUse = use.InUse;
        WhenLabel = use.InUse
            ? "In use now"
            : string.Create(CultureInfo.CurrentCulture, $"{use.LastStarted!.Value.LocalDateTime:g}");
        KindLabel = use.IsDesktopApp ? "Desktop app" : "Store app";
    }

    /// <summary>Camera, Microphone, Location, or the capability name.</summary>
    public string CapabilityLabel { get; }

    /// <summary>The app (package name or executable path).</summary>
    public string App { get; }

    /// <summary>Whether it is using the capability right now.</summary>
    public bool InUse { get; }

    /// <summary>When it last started using it, or "In use now".</summary>
    public string WhenLabel { get; }

    /// <summary>Desktop app or Store app.</summary>
    public string KindLabel { get; }

    /// <summary>Spoken by screen readers for the list item.</summary>
    public override string ToString() => string.Create(CultureInfo.CurrentCulture, $"{CapabilityLabel}, {WhenLabel}, {App}");
}
